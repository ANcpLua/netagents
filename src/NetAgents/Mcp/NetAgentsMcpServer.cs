using System.ComponentModel;
using System.Text.Json;
using NetAgents.Cli.Commands;
using NetAgents.Config;
using Qyl.Agents;

namespace NetAgents.Mcp;

[McpServer("netagents", Version = "1.2.0")]
public partial class NetAgentsMcpServer
{
    [Tool("list", Description = "List all declared skills and their installation status")]
    public async Task<string> ListAsync(
        [Description("Absolute path to the project root directory")] string projectRoot,
        CancellationToken ct)
    {
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, projectRoot);
        var result = await ListCommand.RunListAsync(new ListOptions(scope), ct).ConfigureAwait(false);
        return FormatJson(result);
    }

    [Tool("install", Description = "Install all skills declared in agents.toml")]
    public async Task<string> InstallAsync(
        [Description("Absolute path to the project root directory")] string projectRoot,
        CancellationToken ct)
    {
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, projectRoot);
        var result = await InstallCommand.RunInstallAsync(new InstallOptions(scope), ct).ConfigureAwait(false);
        return FormatInstallResult(result);
    }

    private static string FormatInstallResult(InstallResult r)
    {
        var parts = new List<string>();
        if (r.Installed.Count > 0) parts.Add($"Installed {r.Installed.Count} skill(s): {string.Join(", ", r.Installed)}");
        if (r.Pruned.Count > 0) parts.Add($"Pruned {r.Pruned.Count} stale skill(s): {string.Join(", ", r.Pruned)}");
        if (r.MissingRootGitignoreEntries.Count > 0) parts.Add($"Warning: {string.Join(", ", r.MissingRootGitignoreEntries)} should be in .gitignore");
        foreach (var w in r.HookWarnings) parts.Add($"warn: {w.Message}");
        return parts.Count > 0 ? string.Join("\n", parts) : "Everything up to date.";
    }

    private static string FormatJson<T>(T value) =>
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
}
