using NetAgents.Agents;
using NetAgents.Config;
using NetAgents.Gitignore;
using NetAgents.Lockfile;
using NetAgents.Skills;
using NetAgents.Symlinks;
using NetAgents.Trust;
using NetAgents.Utils;

namespace NetAgents.Cli.Commands;

public sealed class InstallException(string message) : Exception(message);

public sealed record InstallOptions(ScopeRoot Scope, bool Frozen = false, bool Force = false);

public sealed record InstallResult(
    IReadOnlyList<string> Installed,
    IReadOnlyList<string> Pruned,
    IReadOnlyList<(string Agent, string Message)> HookWarnings);

public static class InstallCommand
{
    private static bool IsInPlaceSkill(string source) =>
        source.StartsWith("path:.agents/skills/", StringComparison.Ordinal) ||
        source.StartsWith("path:skills/", StringComparison.Ordinal);

    public static async Task<InstallResult> RunInstallAsync(InstallOptions opts, CancellationToken ct = default)
    {
        var (scope, frozen, force) = (opts.Scope, opts.Frozen, opts.Force);
        var (configPath, lockPath, agentsDir, skillsDir) = (scope.ConfigPath, scope.LockPath, scope.AgentsDir, scope.SkillsDir);

        var config = await ConfigLoader.LoadAsync(configPath, ct).ConfigureAwait(false);
        var installed = new List<string>();
        var pruned = new List<string>();

        Directory.CreateDirectory(skillsDir);

        if (config.Skills.Count > 0)
        {
            var lockfile = await LockfileLoader.LoadAsync(lockPath, ct).ConfigureAwait(false);

            if (frozen && lockfile is null)
                throw new InstallException("--frozen requires agents.lock to exist.");

            var expanded = await ExpandSkillsAsync(config, lockfile, frozen, force, scope.Root, ct)
                .ConfigureAwait(false);

            if (frozen)
            {
                foreach (var item in expanded)
                {
                    if (!lockfile!.Skills.ContainsKey(item.Name))
                        throw new InstallException(
                            $"--frozen: skill \"{item.Name}\" is in agents.toml but missing from agents.lock.");
                }
            }

            var newLock = new LockfileData(1, new Dictionary<string, LockedSkill>());

            foreach (var item in expanded)
            {
                var sourceForTrust = SkillResolver.ApplyDefaultRepositorySource(
                    item.Dep.Source, config.DefaultRepositorySource);
                TrustValidator.ValidateTrustedSource(sourceForTrust, config.Trust);

                var ttl = force ? TimeSpan.Zero : (TimeSpan?)null;

                ResolvedSkill resolved;
                if (item.Resolved is not null)
                {
                    resolved = item.Resolved;
                }
                else
                {
                    try
                    {
                        var dep = item.Dep is RegularSkillDependency reg
                            ? reg
                            : new RegularSkillDependency(item.Name, item.Dep.Source, item.Dep.Ref, null);
                        resolved = await SkillResolver.ResolveSkillAsync(
                            item.Name, dep, scope.Root, config.DefaultRepositorySource, ttl, ct)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        var msg = ex.Message;
                        throw new InstallException($"Failed to resolve skill \"{item.Name}\": {msg}");
                    }
                }

                var destDir = Path.Combine(skillsDir, item.Name);
                if (Path.GetFullPath(resolved.SkillDir) != Path.GetFullPath(destDir))
                    await FileSystem.CopyDirectoryAsync(resolved.SkillDir, destDir).ConfigureAwait(false);

                LockedSkill lockEntry = resolved switch
                {
                    ResolvedGitSkill g => g.ResolvedRef is not null
                        ? new LockedGitSkill(item.Dep.Source, g.ResolvedUrl, g.ResolvedPath, g.ResolvedRef)
                        : new LockedGitSkill(item.Dep.Source, g.ResolvedUrl, g.ResolvedPath, null),
                    _ => new LockedLocalSkill(item.Dep.Source),
                };

                newLock.Skills[item.Name] = lockEntry;
                installed.Add(item.Name);
            }

            // Prune stale wildcard-sourced skills
            if (!frozen && lockfile is not null)
            {
                var wildcardDeps = config.Skills.OfType<WildcardSkillDependency>().ToList();
                foreach (var (name, locked) in lockfile.Skills)
                {
                    if (newLock.Skills.ContainsKey(name)) continue;
                    var fromWildcard = wildcardDeps.Any(w => SkillResolver.SourcesMatch(locked.Source, w.Source));
                    if (fromWildcard)
                    {
                        var dir = Path.Combine(skillsDir, name);
                        if (Directory.Exists(dir)) Directory.Delete(dir, true);
                        pruned.Add(name);
                    }
                }
            }

            if (!frozen)
                await LockfileWriter.WriteAsync(lockPath, newLock, ct).ConfigureAwait(false);
        }

        // Gitignore
        if (scope.Scope == ScopeKind.Project)
        {
            var managedNames = installed.Where(name =>
            {
                var dep = config.Skills.OfType<RegularSkillDependency>().FirstOrDefault(s => s.Name == name);
                if (dep is null) return true; // wildcard-sourced
                return !IsInPlaceSkill(dep.Source);
            }).ToList();
            await GitignoreWriter.WriteAgentsGitignoreAsync(agentsDir, managedNames, ct).ConfigureAwait(false);

            var missing = await GitignoreWriter.CheckRootGitignoreEntriesAsync(scope.Root, ct).ConfigureAwait(false);
            if (missing.Count > 0)
                Console.WriteLine($"Warning: {string.Join(", ", missing)} should be in .gitignore. Run 'netagents doctor --fix' to fix.");
        }

        // Symlinks
        if (scope.Scope == ScopeKind.User)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var agentId in config.Agents)
            {
                var agent = AgentRegistry.GetAgent(agentId);
                if (agent?.UserSkillsParentDirs is null) continue;
                foreach (var dir in agent.UserSkillsParentDirs)
                {
                    if (!seen.Add(dir)) continue;
                    await SymlinkManager.EnsureSkillsSymlinkAsync(agentsDir, dir, ct).ConfigureAwait(false);
                }
            }
        }
        else
        {
            var targets = config.Symlinks?.Targets ?? [];
            foreach (var target in targets)
                await SymlinkManager.EnsureSkillsSymlinkAsync(agentsDir, Path.Combine(scope.Root, target), ct)
                    .ConfigureAwait(false);

            var seenParentDirs = new HashSet<string>(targets, StringComparer.Ordinal);
            foreach (var agentId in config.Agents)
            {
                var agent = AgentRegistry.GetAgent(agentId);
                if (agent?.SkillsParentDir is null) continue;
                if (!seenParentDirs.Add(agent.SkillsParentDir)) continue;
                await SymlinkManager.EnsureSkillsSymlinkAsync(agentsDir, Path.Combine(scope.Root, agent.SkillsParentDir), ct)
                    .ConfigureAwait(false);
            }
        }

        // MCP configs
        var mcpResolver = scope.Scope == ScopeKind.User
            ? AgentPaths.UserMcpResolver()
            : McpWriter.ProjectResolver(scope.Root);
        await McpWriter.WriteMcpConfigsAsync(config.Agents, McpWriter.ToMcpDeclarations(config.Mcp), mcpResolver, ct)
            .ConfigureAwait(false);

        // Hook configs
        var hookWarnings = new List<(string Agent, string Message)>();
        if (scope.Scope == ScopeKind.Project)
        {
            var warnings = await HookWriter.WriteHookConfigsAsync(
                config.Agents,
                HookWriter.ToHookDeclarations(config.Hooks),
                HookWriter.ProjectResolver(scope.Root), ct).ConfigureAwait(false);
            hookWarnings.AddRange(warnings.Select(w => (w.Agent, w.Message)));
        }

        return new InstallResult(installed, pruned, hookWarnings);
    }

    private sealed record ExpandedSkill(string Name, SkillDependency Dep, ResolvedSkill? Resolved = null);

    private static async Task<List<ExpandedSkill>> ExpandSkillsAsync(
        AgentsConfig config,
        LockfileData? lockfile,
        bool frozen,
        bool force,
        string projectRoot,
        CancellationToken ct)
    {
        var regularDeps = config.Skills.OfType<RegularSkillDependency>().ToList();
        var wildcardDeps = config.Skills.OfType<WildcardSkillDependency>().ToList();
        var explicitNames = new HashSet<string>(regularDeps.Select(d => d.Name), StringComparer.Ordinal);

        var expanded = new List<ExpandedSkill>();

        foreach (var dep in regularDeps)
            expanded.Add(new ExpandedSkill(dep.Name, dep));

        var wildcardNames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var wDep in wildcardDeps)
        {
            var wildcardSourceForTrust = SkillResolver.ApplyDefaultRepositorySource(
                wDep.Source, config.DefaultRepositorySource);
            TrustValidator.ValidateTrustedSource(wildcardSourceForTrust, config.Trust);
            var excludeSet = new HashSet<string>(wDep.Exclude, StringComparer.Ordinal);

            if (frozen)
            {
                if (lockfile is null) continue;
                foreach (var (name, locked) in lockfile.Skills)
                {
                    if (!SkillResolver.SourcesMatch(locked.Source, wDep.Source)) continue;
                    if (explicitNames.Contains(name)) continue;
                    if (excludeSet.Contains(name)) continue;
                    expanded.Add(new ExpandedSkill(name, wDep));
                }
            }
            else
            {
                var ttl = force ? TimeSpan.Zero : (TimeSpan?)null;
                var named = await SkillResolver.ResolveWildcardSkillsAsync(
                    wDep, projectRoot, config.DefaultRepositorySource, ttl, ct).ConfigureAwait(false);
                foreach (var item in named)
                {
                    if (explicitNames.Contains(item.Name)) continue;

                    if (wildcardNames.TryGetValue(item.Name, out var existingSource) &&
                        !SkillResolver.SourcesMatch(existingSource, wDep.Source))
                    {
                        throw new InstallException(
                            $"Skill \"{item.Name}\" found in both wildcard sources: \"{existingSource}\" and \"{wDep.Source}\". " +
                            "Use an explicit [[skills]] entry or add it to one source's exclude list.");
                    }

                    wildcardNames[item.Name] = wDep.Source;
                    expanded.Add(new ExpandedSkill(item.Name, wDep, item.Skill));
                }
            }
        }

        return expanded;
    }

    public static async Task<int> ExecuteAsync(string[] args, bool isUser, CancellationToken ct = default)
    {
        var frozen = args.Contains("--frozen");
        var force = args.Contains("--force");

        try
        {
            var scope = isUser
                ? ScopeResolver.ResolveScope(ScopeKind.User)
                : ScopeResolver.ResolveDefaultScope(Path.GetFullPath("."));
            await EnsureUserScope.EnsureUserScopeBootstrappedAsync(scope, ct).ConfigureAwait(false);

            if (frozen)
                Console.WriteLine("Warning: --frozen is deprecated and will be removed in a future release. Pinning is now managed via agents.toml refs.");

            var result = await RunInstallAsync(new InstallOptions(scope, frozen, force), ct).ConfigureAwait(false);

            if (result.Installed.Count > 0)
                Console.WriteLine($"Installed {result.Installed.Count} skill(s): {string.Join(", ", result.Installed)}");
            if (result.Pruned.Count > 0)
                Console.WriteLine($"Pruned {result.Pruned.Count} stale skill(s): {string.Join(", ", result.Pruned)}");
            foreach (var w in result.HookWarnings)
                Console.WriteLine($"  warn: {w.Message}");
            return 0;
        }
        catch (Exception ex) when (ex is ScopeException or InstallException or TrustException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
