using NetAgents.Config;

namespace NetAgents.Cli.Commands;

public sealed class TrustCommandException(string message) : Exception(message);

public static class TrustCommand
{
    public static (string Field, string Value) ClassifyTrustSource(string source)
    {
        if (source.Contains('/')) return ("github_repos", source);
        return source.Contains('.') ? ("git_domains", source) : ("github_orgs", source);
    }

    public static async Task RunTrustAddAsync(ScopeRoot scope, string source, CancellationToken ct = default)
    {
        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, ct).ConfigureAwait(false);
        var (field, value) = ClassifyTrustSource(source);

        var existing = field switch
        {
            "github_orgs" => config.Trust?.GithubOrgs ?? [],
            "github_repos" => config.Trust?.GithubRepos ?? [],
            "git_domains" => config.Trust?.GitDomains ?? [],
            _ => (IReadOnlyList<string>)[],
        };

        if (existing.Any(e => string.Equals(e, value, StringComparison.OrdinalIgnoreCase)))
            throw new TrustCommandException($"\"{value}\" is already in {field}.");

        await ConfigWriter.AddTrustSourceAsync(scope.ConfigPath, field, value, ct).ConfigureAwait(false);
    }

    public static async Task RunTrustRemoveAsync(ScopeRoot scope, string source, CancellationToken ct = default)
    {
        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, ct).ConfigureAwait(false);
        var (field, value) = ClassifyTrustSource(source);

        var existing = field switch
        {
            "github_orgs" => config.Trust?.GithubOrgs ?? [],
            "github_repos" => config.Trust?.GithubRepos ?? [],
            "git_domains" => config.Trust?.GitDomains ?? [],
            _ => (IReadOnlyList<string>)[],
        };

        if (!existing.Any(e => string.Equals(e, value, StringComparison.OrdinalIgnoreCase)))
            throw new TrustCommandException($"\"{value}\" not found in {field}.");

        await ConfigWriter.RemoveTrustSourceAsync(scope.ConfigPath, field, value, ct).ConfigureAwait(false);
    }

    public sealed record TrustListEntry(string Type, string Value);

    public static object GetTrustList(AgentsConfig config)
    {
        if (config.Trust is null) return Array.Empty<TrustListEntry>();
        if (config.Trust.AllowAll) return "allow_all";

        var entries = new List<TrustListEntry>();
        foreach (var org in config.Trust.GithubOrgs)
            entries.Add(new TrustListEntry("github_org", org));
        foreach (var repo in config.Trust.GithubRepos)
            entries.Add(new TrustListEntry("github_repo", repo));
        foreach (var domain in config.Trust.GitDomains)
            entries.Add(new TrustListEntry("git_domain", domain));
        return entries;
    }

    public static async Task<int> ExecuteAsync(string[] args, bool isUser, CancellationToken ct = default)
    {
        var sub = args.FirstOrDefault();
        if (sub is null or "--help" or "-h")
        {
            PrintTrustUsage();
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
                "add" => await TrustAddCliAsync(subArgs, scope, ct).ConfigureAwait(false),
                "remove" => await TrustRemoveCliAsync(subArgs, scope, ct).ConfigureAwait(false),
                "list" => await TrustListCliAsync(subArgs, scope, ct).ConfigureAwait(false),
                _ => UnknownSubcommand(sub),
            };
        }
        catch (TrustCommandException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static async Task<int> TrustAddCliAsync(string[] args, ScopeRoot scope, CancellationToken ct)
    {
        var source = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (source is null)
        {
            Console.Error.WriteLine("Usage: netagents trust add <source>");
            Console.Error.WriteLine("  <source> can be: org, owner/repo, or domain.name");
            return 1;
        }

        var (field, _) = ClassifyTrustSource(source);
        await RunTrustAddAsync(scope, source, ct).ConfigureAwait(false);
        Console.WriteLine($"Added \"{source}\" to {field}.");
        return 0;
    }

    private static async Task<int> TrustRemoveCliAsync(string[] args, ScopeRoot scope, CancellationToken ct)
    {
        var source = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (source is null)
        {
            Console.Error.WriteLine("Usage: netagents trust remove <source>");
            return 1;
        }

        var (field, _) = ClassifyTrustSource(source);
        await RunTrustRemoveAsync(scope, source, ct).ConfigureAwait(false);
        Console.WriteLine($"Removed \"{source}\" from {field}.");
        return 0;
    }

    private static async Task<int> TrustListCliAsync(string[] args, ScopeRoot scope, CancellationToken ct)
    {
        var json = args.Contains("--json");
        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, ct).ConfigureAwait(false);
        var entries = GetTrustList(config);

        if (json)
        {
            if (entries is string and "allow_all")
            {
                var node = new System.Text.Json.Nodes.JsonObject { ["allow_all"] = true };
                Console.WriteLine(node.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(
                    (IReadOnlyList<TrustListEntry>)entries,
                    NetAgentsJsonContext.Default.IReadOnlyListTrustListEntry));
            }
            return 0;
        }

        if (entries is string and "allow_all")
        {
            Console.WriteLine("allow_all = true -- all sources are trusted.");
            return 0;
        }

        if (entries is IReadOnlyList<TrustListEntry> { Count: 0 })
        {
            Console.WriteLine("No trust rules declared in agents.toml.");
            return 0;
        }

        Console.WriteLine("Trusted sources:");
        foreach (var e in (IReadOnlyList<TrustListEntry>)entries)
            Console.WriteLine($"  {e.Value}  {e.Type}");

        return 0;
    }

    private static void PrintTrustUsage() =>
        Console.Error.WriteLine("""
            Usage: netagents trust <subcommand>

            Subcommands:
              add      Add a trusted source (org, owner/repo, or domain)
              remove   Remove a trusted source
              list     Show trusted sources
            """);

    private static int UnknownSubcommand(string sub)
    {
        Console.Error.WriteLine($"Unknown trust subcommand: {sub}");
        PrintTrustUsage();
        return 1;
    }
}
