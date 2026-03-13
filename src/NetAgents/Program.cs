using System.Reflection;
using NetAgents.Cli;
using NetAgents.Cli.Commands;
using NetAgents.Mcp;
using Qyl.Agents.Hosting;

var rawArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();

// Extract --user flag before command dispatch
var userIndex = Array.IndexOf(rawArgs, "--user");
var isUser = userIndex >= 0;
if (isUser)
    rawArgs = [.. rawArgs[..userIndex], .. rawArgs[(userIndex + 1)..]];

var ct = CancellationToken.None;
var command = rawArgs.FirstOrDefault();

try
{
    return command switch
    {
        null or "--help" or "-h" => PrintUsage(),
        "--version" or "-V" => PrintVersion(),
        "init" => await InitCommand.ExecuteAsync(rawArgs[1..], isUser, ct),
        "install" => await InstallCommand.ExecuteAsync(rawArgs[1..], isUser, ct),
        "add" => await AddCommand.ExecuteAsync(rawArgs[1..], isUser, ct),
        "remove" => await RemoveCommand.ExecuteAsync(rawArgs[1..], isUser, ct),
        "sync" => await SyncCommand.ExecuteAsync(rawArgs[1..], isUser, ct),
        "list" => await ListCommand.ExecuteAsync(rawArgs[1..], isUser, ct),
        "mcp" when rawArgs.ElementAtOrDefault(1) == "serve" => await RunMcpServe(ct),
        "mcp" => await McpCommand.ExecuteAsync(rawArgs[1..], isUser, ct),
        "trust" => await TrustCommand.ExecuteAsync(rawArgs[1..], isUser, ct),
        "doctor" => await DoctorCommand.ExecuteAsync(rawArgs[1..], isUser, ct),
        _ => UnknownCommand(command)
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static int PrintUsage()
{
    Console.WriteLine("""
                      netagents - package manager for .agents directories

                      Usage: netagents [--user] <command> [options]

                      Commands:
                        init        Initialize agents.toml and .agents/skills/
                        install     Install dependencies from agents.toml
                        add         Add a skill dependency
                        remove      Remove a skill dependency
                        sync        Reconcile gitignore, symlinks, verify state
                        list        Show installed skills
                        mcp         Manage MCP server declarations
                        trust       Manage trusted sources
                        doctor      Check project health and fix issues

                      Options:
                        --user      Operate on user-scope (~/.agents/) instead of project
                        --help, -h  Show this help message
                        --version   Show version
                      """);
    return 0;
}

static int PrintVersion()
{
    var version = typeof(EnsureUserScope).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
    Console.WriteLine(version);
    return 0;
}

static int UnknownCommand(string name)
{
    Console.Error.WriteLine($"Unknown command: {name}");
    PrintUsage();
    return 1;
}

static async Task<int> RunMcpServe(CancellationToken ct)
{
    var server = new NetAgentsMcpServer();
    await McpHost.RunStdioAsync(server, ct);
    return 0;
}
