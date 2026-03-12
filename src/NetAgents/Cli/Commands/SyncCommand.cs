using NetAgents.Agents;
using NetAgents.Config;
using NetAgents.Gitignore;
using NetAgents.Lockfile;
using NetAgents.Skills;
using NetAgents.Symlinks;

namespace NetAgents.Cli.Commands;

public sealed record SyncIssue(string Type, string Name, string Message);

public sealed record SyncOptions(ScopeRoot Scope);

public sealed record SyncResult(
    IReadOnlyList<SyncIssue> Issues,
    IReadOnlyList<string> Adopted,
    bool GitignoreUpdated,
    int SymlinksRepaired,
    int McpRepaired,
    int HooksRepaired,
    IReadOnlyList<string> MissingRootGitignoreEntries);

public static class SyncCommand
{
    private static bool IsInPlaceSkill(string source) =>
        source.StartsWith("path:.agents/skills/", StringComparison.Ordinal) ||
        source.StartsWith("path:skills/", StringComparison.Ordinal);

    public static async Task<SyncResult> RunSyncAsync(SyncOptions opts, CancellationToken ct = default)
    {
        var scope = opts.Scope;
        var (configPath, lockPath, agentsDir, skillsDir) = (scope.ConfigPath, scope.LockPath, scope.AgentsDir, scope.SkillsDir);

        var config = await ConfigLoader.LoadAsync(configPath, ct).ConfigureAwait(false);
        var lockfile = await LockfileLoader.LoadAsync(lockPath, ct).ConfigureAwait(false);

        var declaredNames = new HashSet<string>(
            config.Skills.OfType<RegularSkillDependency>().Select(s => s.Name), StringComparer.Ordinal);

        if (lockfile is not null)
        {
            var wildcardSources = new HashSet<string>(
                config.Skills.OfType<WildcardSkillDependency>().Select(s => SkillResolver.NormalizeSource(s.Source)),
                StringComparer.Ordinal);
            foreach (var (name, locked) in lockfile.Skills)
            {
                if (wildcardSources.Contains(SkillResolver.NormalizeSource(locked.Source)))
                    declaredNames.Add(name);
            }
        }

        var issues = new List<SyncIssue>();
        var adopted = new List<string>();

        // 1. Adopt orphaned skills
        if (Directory.Exists(skillsDir))
        {
            var adoptedLockEntries = new Dictionary<string, LockedSkill>();
            foreach (var entry in new DirectoryInfo(skillsDir).EnumerateDirectories())
            {
                if (declaredNames.Contains(entry.Name)) continue;

                var sourcePrefix = scope.Scope == ScopeKind.User ? "path:skills/" : "path:.agents/skills/";
                var source = $"{sourcePrefix}{entry.Name}";
                await ConfigWriter.AddSkillToConfigAsync(configPath, entry.Name, source, ct: ct)
                    .ConfigureAwait(false);
                declaredNames.Add(entry.Name);
                adoptedLockEntries[entry.Name] = new LockedLocalSkill(source);
                adopted.Add(entry.Name);
            }

            if (adopted.Count > 0)
            {
                var mergedSkills = new Dictionary<string, LockedSkill>(lockfile?.Skills ?? new Dictionary<string, LockedSkill>());
                foreach (var (name, entry) in adoptedLockEntries)
                    mergedSkills[name] = entry;
                await LockfileWriter.WriteAsync(lockPath, new LockfileData(1, mergedSkills), ct)
                    .ConfigureAwait(false);
                config = await ConfigLoader.LoadAsync(configPath, ct).ConfigureAwait(false);
            }
        }

        // 2. Regenerate .agents/.gitignore
        var gitignoreUpdated = false;
        IReadOnlyList<string> missingGitignore = [];
        if (scope.Scope == ScopeKind.Project)
        {
            var lockNow = await LockfileLoader.LoadAsync(lockPath, ct).ConfigureAwait(false);
            var allNames = lockNow is not null
                ? lockNow.Skills.Keys.ToList()
                : config.Skills.OfType<RegularSkillDependency>().Select(s => s.Name).ToList();
            var managedNames = allNames.Where(name =>
            {
                var dep = config.Skills.OfType<RegularSkillDependency>().FirstOrDefault(s => s.Name == name);
                if (dep is null) return true;
                return !IsInPlaceSkill(dep.Source);
            }).ToList();
            await GitignoreWriter.WriteAgentsGitignoreAsync(agentsDir, managedNames, ct).ConfigureAwait(false);
            gitignoreUpdated = true;

            missingGitignore = await GitignoreWriter.CheckRootGitignoreEntriesAsync(scope.Root, ct).ConfigureAwait(false);
        }

        // 3. Check for missing skills
        foreach (var name in declaredNames)
        {
            if (!Directory.Exists(Path.Combine(skillsDir, name)))
                issues.Add(new SyncIssue("missing", name,
                    $"\"{name}\" is in agents.toml but not installed. Run 'netagents install'."));
        }

        // 4. Verify and repair symlinks
        var symlinksRepaired = 0;

        if (scope.Scope == ScopeKind.User)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var targets = new List<string>();
            foreach (var agentId in config.Agents)
            {
                var agent = AgentRegistry.GetAgent(agentId);
                if (agent?.UserSkillsParentDirs is null) continue;
                foreach (var dir in agent.UserSkillsParentDirs)
                {
                    if (!seen.Add(dir)) continue;
                    targets.Add(dir);
                }
            }

            var symlinkIssues = await SymlinkManager.VerifySymlinksAsync(agentsDir, targets, ct).ConfigureAwait(false);
            foreach (var issue in symlinkIssues)
            {
                await SymlinkManager.EnsureSkillsSymlinkAsync(agentsDir, issue.Target, ct).ConfigureAwait(false);
                symlinksRepaired++;
            }
        }
        else
        {
            var legacyTargets = config.Symlinks?.Targets ?? [];
            var legacyIssues = await SymlinkManager.VerifySymlinksAsync(
                agentsDir, legacyTargets.Select(t => Path.Combine(scope.Root, t)).ToList(), ct).ConfigureAwait(false);
            foreach (var issue in legacyIssues)
            {
                await SymlinkManager.EnsureSkillsSymlinkAsync(agentsDir, Path.Combine(scope.Root, issue.Target), ct)
                    .ConfigureAwait(false);
                symlinksRepaired++;
            }

            var seenParentDirs = new HashSet<string>(legacyTargets, StringComparer.Ordinal);
            var agentTargets = new List<string>();
            foreach (var agentId in config.Agents)
            {
                var agent = AgentRegistry.GetAgent(agentId);
                if (agent?.SkillsParentDir is null) continue;
                if (!seenParentDirs.Add(agent.SkillsParentDir)) continue;
                agentTargets.Add(Path.Combine(scope.Root, agent.SkillsParentDir));
            }

            var agentSymlinkIssues = await SymlinkManager.VerifySymlinksAsync(agentsDir, agentTargets, ct)
                .ConfigureAwait(false);
            foreach (var issue in agentSymlinkIssues)
            {
                await SymlinkManager.EnsureSkillsSymlinkAsync(agentsDir, issue.Target, ct).ConfigureAwait(false);
                symlinksRepaired++;
            }
        }

        // 5. Verify and repair MCP configs
        var mcpRepaired = 0;
        var mcpServers = McpWriter.ToMcpDeclarations(config.Mcp);
        var mcpResolver = scope.Scope == ScopeKind.User
            ? AgentPaths.UserMcpResolver()
            : McpWriter.ProjectResolver(scope.Root);

        var mcpIssues = await McpWriter.VerifyMcpConfigsAsync(config.Agents, mcpServers, mcpResolver, ct)
            .ConfigureAwait(false);
        if (mcpIssues.Count > 0)
        {
            await McpWriter.WriteMcpConfigsAsync(config.Agents, mcpServers, mcpResolver, ct).ConfigureAwait(false);
            mcpRepaired = mcpIssues.Count;
            foreach (var issue in mcpIssues)
                issues.Add(new SyncIssue("mcp", issue.Agent, issue.Issue));
        }

        // 6. Verify and repair hook configs
        var hooksRepaired = 0;
        if (scope.Scope == ScopeKind.Project)
        {
            var hookDecls = HookWriter.ToHookDeclarations(config.Hooks);
            var hookResolver = HookWriter.ProjectResolver(scope.Root);

            var hookIssues = await HookWriter.VerifyHookConfigsAsync(config.Agents, hookDecls, hookResolver, ct)
                .ConfigureAwait(false);
            if (hookIssues.Count > 0)
            {
                await HookWriter.WriteHookConfigsAsync(config.Agents, hookDecls, hookResolver, ct)
                    .ConfigureAwait(false);
                hooksRepaired = hookIssues.Count;
                foreach (var issue in hookIssues)
                    issues.Add(new SyncIssue("hooks", issue.Agent, issue.Issue));
            }
        }

        return new SyncResult(issues, adopted, gitignoreUpdated, symlinksRepaired, mcpRepaired, hooksRepaired, missingGitignore);
    }

    public static async Task<int> ExecuteAsync(string[] args, bool isUser, CancellationToken ct = default)
    {
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

        var result = await RunSyncAsync(new SyncOptions(scope), ct).ConfigureAwait(false);

        if (result.MissingRootGitignoreEntries.Count > 0)
            Console.WriteLine($"Warning: {string.Join(", ", result.MissingRootGitignoreEntries)} should be in .gitignore. Run 'netagents doctor --fix' to fix.");

        if (result.Adopted.Count > 0)
            Console.WriteLine($"Adopted {result.Adopted.Count} orphan(s): {string.Join(", ", result.Adopted)}");
        if (scope.Scope == ScopeKind.Project && result.GitignoreUpdated)
            Console.WriteLine("Regenerated .agents/.gitignore");
        if (result.SymlinksRepaired > 0)
            Console.WriteLine($"Repaired {result.SymlinksRepaired} symlink(s)");
        if (result.McpRepaired > 0)
            Console.WriteLine($"Repaired {result.McpRepaired} MCP config(s)");
        if (result.HooksRepaired > 0)
            Console.WriteLine($"Repaired {result.HooksRepaired} hook config(s)");

        if (result.Issues.Count == 0)
        {
            Console.WriteLine("Everything in sync.");
            return 0;
        }

        foreach (var issue in result.Issues)
        {
            switch (issue.Type)
            {
                case "mcp" or "hooks":
                    Console.WriteLine($"  warn: {issue.Message}");
                    break;
                case "missing":
                    Console.WriteLine($"  error: {issue.Message}");
                    break;
            }
        }

        return 0;
    }
}
