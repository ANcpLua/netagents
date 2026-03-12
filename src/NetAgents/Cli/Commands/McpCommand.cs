using System.Text.RegularExpressions;
using NetAgents.Config;

namespace NetAgents.Cli.Commands;

public sealed class McpException(string message) : Exception(message);

public sealed class McpCancelledException() : Exception("Cancelled");

public static partial class McpCommand
{
    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9._-]*$")]
    private static partial Regex McpNamePattern();

    public static void ValidateMcpName(string name)
    {
        if (!McpNamePattern().IsMatch(name))
            throw new McpException(
                $"Invalid MCP server name \"{name}\". Names must start with alphanumeric and contain only [a-zA-Z0-9._-].");
    }

    public static (string Key, string Value) ParseHeader(string raw)
    {
        var idx = raw.IndexOf(':');
        if (idx < 1)
            throw new McpException($"Invalid header format: \"{raw}\". Expected \"Key:Value\".");
        return (raw[..idx], raw[(idx + 1)..]);
    }

    public sealed record McpAddOptions(
        ScopeRoot Scope,
        string Name,
        string? Command = null,
        IReadOnlyList<string>? Args = null,
        string? Url = null,
        IReadOnlyList<string>? Headers = null,
        IReadOnlyList<string>? Env = null);

    public static async Task RunMcpAddAsync(McpAddOptions opts, CancellationToken ct = default)
    {
        ValidateMcpName(opts.Name);

        if (opts.Command is not null && opts.Url is not null)
            throw new McpException("Cannot specify both --command and --url.");
        if (opts.Command is null && opts.Url is null)
            throw new McpException("Must specify either --command or --url.");

        var config = await ConfigLoader.LoadAsync(opts.Scope.ConfigPath, ct).ConfigureAwait(false);
        if (config.Mcp.Any(m => m.Name == opts.Name))
            throw new McpException($"MCP server \"{opts.Name}\" already exists in agents.toml. Remove it first.");

        IReadOnlyDictionary<string, string>? headers = null;
        if (opts.Headers is { Count: > 0 })
        {
            var dict = new Dictionary<string, string>();
            foreach (var h in opts.Headers)
            {
                var (key, value) = ParseHeader(h);
                dict[key] = value;
            }
            headers = dict;
        }

        var entry = new McpConfig(
            opts.Name, opts.Command, opts.Args, opts.Url, headers, opts.Env ?? []);
        await ConfigWriter.AddMcpToConfigAsync(opts.Scope.ConfigPath, entry, ct).ConfigureAwait(false);
        await InstallCommand.RunInstallAsync(new InstallOptions(opts.Scope), ct).ConfigureAwait(false);
    }

    public sealed record McpRemoveOptions(ScopeRoot Scope, string Name);

    public static async Task RunMcpRemoveAsync(McpRemoveOptions opts, CancellationToken ct = default)
    {
        var config = await ConfigLoader.LoadAsync(opts.Scope.ConfigPath, ct).ConfigureAwait(false);
        if (!config.Mcp.Any(m => m.Name == opts.Name))
            throw new McpException($"MCP server \"{opts.Name}\" not found in agents.toml.");

        await ConfigWriter.RemoveMcpFromConfigAsync(opts.Scope.ConfigPath, opts.Name, ct).ConfigureAwait(false);
        await InstallCommand.RunInstallAsync(new InstallOptions(opts.Scope), ct).ConfigureAwait(false);
    }

    public sealed record McpListEntry(string Name, string Transport, string Target, IReadOnlyList<string> Env);

    public static IReadOnlyList<McpListEntry> GetMcpList(AgentsConfig config) =>
        config.Mcp.Select(m => new McpListEntry(
            m.Name,
            m.Command is not null ? "stdio" : "http",
            (m.Command ?? m.Url)!,
            m.Env)).ToList();

    public static async Task<int> ExecuteAsync(string[] args, bool isUser, CancellationToken ct = default)
    {
        var sub = args.FirstOrDefault();
        if (sub is null or "--help" or "-h")
        {
            PrintMcpUsage();
            return 0;
        }

        ScopeRoot scope;
        try
        {
            scope = isUser
                ? ScopeResolver.ResolveScope(ScopeKind.User)
                : ScopeResolver.ResolveDefaultScope(Path.GetFullPath("."));
            await EnsureUserScope.EnsureUserScopeBootstrappedAsync(scope, ct).ConfigureAwait(false);
        }
        catch (ScopeException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var subArgs = args.Skip(1).ToArray();
        try
        {
            return sub switch
            {
                "add" => await McpAddCliAsync(subArgs, scope, ct).ConfigureAwait(false),
                "remove" => await McpRemoveCliAsync(subArgs, scope, ct).ConfigureAwait(false),
                "list" => await McpListCliAsync(subArgs, scope, ct).ConfigureAwait(false),
                _ => UnknownSubcommand(sub),
            };
        }
        catch (McpCancelledException) { return 0; }
        catch (Exception ex) when (ex is ScopeException or McpException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static async Task<int> McpAddCliAsync(string[] args, ScopeRoot scope, CancellationToken ct)
    {
        string? name = null, command = null, url = null;
        var headers = new List<string>();
        var envVars = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--command" when i + 1 < args.Length: command = args[++i]; break;
                case "--url" when i + 1 < args.Length: url = args[++i]; break;
                case "--header" when i + 1 < args.Length: headers.Add(args[++i]); break;
                case "--env" when i + 1 < args.Length: envVars.Add(args[++i]); break;
                default:
                    if (!args[i].StartsWith('-')) name ??= args[i];
                    break;
            }
        }

        if (name is null)
        {
            Console.Error.WriteLine("Usage: netagents mcp add <name> --command <cmd> [--env <VAR>...]");
            return 1;
        }

        string? parsedCommand = null;
        IReadOnlyList<string>? parsedArgs = null;
        if (command is not null)
        {
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            parsedCommand = parts[0];
            parsedArgs = parts.Length > 1 ? parts[1..] : null;
        }

        await RunMcpAddAsync(new McpAddOptions(scope, name, parsedCommand, parsedArgs, url,
            headers.Count > 0 ? headers : null, envVars.Count > 0 ? envVars : null), ct).ConfigureAwait(false);
        Console.WriteLine($"Added MCP server: {name}");
        return 0;
    }

    private static async Task<int> McpRemoveCliAsync(string[] args, ScopeRoot scope, CancellationToken ct)
    {
        var name = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (name is null)
        {
            Console.Error.WriteLine("Usage: netagents mcp remove <name>");
            return 1;
        }

        await RunMcpRemoveAsync(new McpRemoveOptions(scope, name), ct).ConfigureAwait(false);
        Console.WriteLine($"Removed MCP server: {name}");
        return 0;
    }

    private static async Task<int> McpListCliAsync(string[] args, ScopeRoot scope, CancellationToken ct)
    {
        var json = args.Contains("--json");
        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, ct).ConfigureAwait(false);
        var entries = GetMcpList(config);

        if (entries.Count == 0)
        {
            Console.WriteLine("No MCP servers declared in agents.toml.");
            return 0;
        }

        if (json)
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(entries,
                NetAgentsJsonContext.Default.IReadOnlyListMcpListEntry));
            return 0;
        }

        Console.WriteLine("MCP servers:");
        foreach (var e in entries)
        {
            var env = e.Env.Count > 0 ? $" env=[{string.Join(",", e.Env)}]" : "";
            Console.WriteLine($"  {e.Name}  {e.Transport}  {e.Target}{env}");
        }

        return 0;
    }

    private static void PrintMcpUsage() =>
        Console.Error.WriteLine("""
            Usage: netagents mcp <subcommand>

            Subcommands:
              add      Add an MCP server declaration
              remove   Remove an MCP server declaration
              list     Show declared MCP servers
            """);

    private static int UnknownSubcommand(string sub)
    {
        Console.Error.WriteLine($"Unknown mcp subcommand: {sub}");
        PrintMcpUsage();
        return 1;
    }
}
