namespace NetAgents.Mcp;

using System.ComponentModel;
using System.Text.Json;
using Cli.Commands;
using Qyl.Agents;

[McpServer("netagents", Version = "1.2.0")]
public partial class NetAgentsMcpServer
{
    [Tool("list", Description = "List all declared skills and their installation status")]
    public async Task<string> ListAsync(
        [Description("Absolute path to the project root directory")]
        string projectRoot,
        CancellationToken ct)
    {
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, projectRoot);
        var result = await ListCommand.RunListAsync(new ListOptions(scope), ct).ConfigureAwait(false);
        return FormatJson(result);
    }

    [Tool("install", Description = "Install all skills declared in agents.toml")]
    public async Task<string> InstallAsync(
        [Description("Absolute path to the project root directory")]
        string projectRoot,
        CancellationToken ct)
    {
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, projectRoot);
        var result = await InstallCommand.RunInstallAsync(new InstallOptions(scope), ct).ConfigureAwait(false);
        return FormatInstallResult(result);
    }

    [Tool("add", Description = "Add a skill dependency to agents.toml")]
    public async Task<string> AddAsync(
        [Description("Absolute path to the project root directory")]
        string projectRoot,
        [Description("Skill source specifier (owner/repo, git:url, or path:relative)")]
        string skillSource,
        CancellationToken ct)
    {
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, projectRoot);
        var result = await AddCommand.RunAddAsync(new AddOptions(scope, skillSource, Interactive: false), ct)
            .ConfigureAwait(false);
        return FormatAddResult(result);
    }

    [Tool("remove", Description = "Remove a skill from agents.toml")]
    public async Task<string> RemoveAsync(
        [Description("Absolute path to the project root directory")]
        string projectRoot,
        [Description("Name of the skill to remove")]
        string skillName,
        CancellationToken ct)
    {
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, projectRoot);
        var result = await RemoveCommand.RunRemoveAsync(new RemoveOptions(scope, skillName), ct)
            .ConfigureAwait(false);
        return FormatRemoveResult(result);
    }

    [Tool("sync", Description = "Reconcile installed skills with agents.toml")]
    public async Task<string> SyncAsync(
        [Description("Absolute path to the project root directory")]
        string projectRoot,
        CancellationToken ct)
    {
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, projectRoot);
        var result = await SyncCommand.RunSyncAsync(new SyncOptions(scope), ct).ConfigureAwait(false);
        return FormatSyncResult(result);
    }

    [Tool("doctor", Description = "Run health checks on the agents configuration")]
    public async Task<string> DoctorAsync(
        [Description("Absolute path to the project root directory")]
        string projectRoot,
        [Description("Whether to automatically fix issues that can be resolved")]
        bool fix,
        CancellationToken ct)
    {
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, projectRoot);
        var result = await DoctorCommand.RunDoctorAsync(new DoctorOptions(scope, fix), ct)
            .ConfigureAwait(false);
        return FormatDoctorResult(result);
    }

    private static string FormatInstallResult(InstallResult r)
    {
        var parts = new List<string>();
        if (r.Installed.Count > 0)
            parts.Add($"Installed {r.Installed.Count} skill(s): {string.Join(", ", r.Installed)}");
        if (r.Pruned.Count > 0) parts.Add($"Pruned {r.Pruned.Count} stale skill(s): {string.Join(", ", r.Pruned)}");
        if (r.MissingRootGitignoreEntries.Count > 0)
            parts.Add($"Warning: {string.Join(", ", r.MissingRootGitignoreEntries)} should be in .gitignore");
        foreach (var w in r.HookWarnings) parts.Add($"warn: {w.Message}");
        return parts.Count > 0 ? string.Join("\n", parts) : "Everything up to date.";
    }

    private static string FormatAddResult(AddResult r)
    {
        return r switch
        {
            { IsWildcard: true } => "Added all skills from source.",
            { MultipleNames: not null } => $"Added skills: {string.Join(", ", r.MultipleNames)}",
            _ => $"Added skill: {r.SingleName}"
        };
    }

    private static string FormatRemoveResult(RemoveResult r)
    {
        return r switch
        {
            { IsWildcard: true } => $"{r.Hint}",
            { Removed: true } => $"Removed skill: {r.SkillName}",
            _ => $"Could not remove skill: {r.SkillName}"
        };
    }

    private static string FormatSyncResult(SyncResult r)
    {
        var parts = new List<string>();
        if (r.Adopted.Count > 0) parts.Add($"Adopted {r.Adopted.Count} orphan(s): {string.Join(", ", r.Adopted)}");
        if (r.GitignoreUpdated) parts.Add("Regenerated .agents/.gitignore");
        if (r.SymlinksRepaired > 0) parts.Add($"Repaired {r.SymlinksRepaired} symlink(s)");
        if (r.McpRepaired > 0) parts.Add($"Repaired {r.McpRepaired} MCP config(s)");
        if (r.HooksRepaired > 0) parts.Add($"Repaired {r.HooksRepaired} hook config(s)");
        if (r.Issues.Count > 0)
            foreach (var i in r.Issues)
                parts.Add($"{i.Type}: {i.Message}");
        return parts.Count > 0 ? string.Join("\n", parts) : "Everything in sync.";
    }

    private static string FormatDoctorResult(DoctorResult r)
    {
        var parts = new List<string>();
        foreach (var c in r.Checks)
            parts.Add($"[{c.Status}] {c.Name}: {c.Message}");
        if (r.Fixed > 0) parts.Add($"Fixed {r.Fixed} issue(s).");
        return parts.Count > 0 ? string.Join("\n", parts) : "All checks passed.";
    }

    private static string FormatJson(IReadOnlyList<SkillStatus> value)
    {
        return JsonSerializer.Serialize(value, NetAgentsJsonContext.Default.IReadOnlyListSkillStatus);
    }
}
