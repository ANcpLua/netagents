using NetAgents.Agents;
using NetAgents.Config;
using NetAgents.Gitignore;
using NetAgents.Symlinks;
using NetAgents.Trust;

namespace NetAgents.Cli.Commands;

public sealed class InitException(string message) : Exception(message);

public sealed record InitOptions(
    ScopeRoot Scope,
    bool Force = false,
    IReadOnlyList<string>? Agents = null,
    TrustConfig? Trust = null,
    IReadOnlyList<SkillEntry>? Skills = null);

public static class InitCommand
{
    private static readonly SkillEntry BootstrapSkill = new("netagents", "getsentry/dotagents");

    private const string PostMergeMarker = "# netagents:post-merge";

    private static readonly string PostMergeSnippet = $"""

        {PostMergeMarker}
        if command -v netagents >/dev/null 2>&1; then
          netagents install
        elif command -v dotnet >/dev/null 2>&1; then
          dotnet tool run netagents install
        fi
        # netagents:end
        """;

    public static async Task RunInitAsync(InitOptions opts, CancellationToken ct = default)
    {
        var (scope, force, agents, trust, skills) = (opts.Scope, opts.Force, opts.Agents, opts.Trust, opts.Skills);
        var (configPath, agentsDir, skillsDir) = (scope.ConfigPath, scope.AgentsDir, scope.SkillsDir);
        var effectiveSkills = skills ?? [BootstrapSkill];

        if (File.Exists(configPath) && !force)
            throw new InitException("agents.toml already exists. Use --force to overwrite.");

        // Validate agent IDs
        var validIds = AgentRegistry.AllAgentIds();
        if (agents is not null)
        {
            var unknown = agents.Where(id => !validIds.Contains(id)).ToList();
            if (unknown.Count > 0)
                throw new InitException(
                    $"Unknown agent(s): {string.Join(", ", unknown)}. Valid agents: {string.Join(", ", validIds)}");
        }

        // Auto-whitelist bootstrap skill source in restricted trust
        var effectiveTrust = trust;
        if (effectiveTrust is { AllowAll: false } && effectiveSkills.Any(s => s.Source == "getsentry/dotagents"))
        {
            var alreadyCovered =
                effectiveTrust.GithubOrgs.Any(o => string.Equals(o, "getsentry", StringComparison.OrdinalIgnoreCase)) ||
                effectiveTrust.GithubRepos.Any(r => string.Equals(r, "getsentry/dotagents", StringComparison.OrdinalIgnoreCase));
            if (!alreadyCovered)
                effectiveTrust = effectiveTrust with
                {
                    GithubRepos = [.. effectiveTrust.GithubRepos, "getsentry/dotagents"]
                };
        }

        Directory.CreateDirectory(agentsDir);
        await File.WriteAllTextAsync(configPath,
            ConfigWriter.GenerateDefaultConfig(new DefaultConfigOptions(agents, effectiveTrust, effectiveSkills)), ct)
            .ConfigureAwait(false);
        Directory.CreateDirectory(skillsDir);

        var config = await ConfigLoader.LoadAsync(configPath, ct).ConfigureAwait(false);

        if (scope.Scope == ScopeKind.Project)
        {
            await GitignoreWriter.WriteAgentsGitignoreAsync(agentsDir, [], ct).ConfigureAwait(false);
            await GitignoreWriter.EnsureRootGitignoreEntriesAsync(scope.Root, ct).ConfigureAwait(false);
        }

        // Symlinks
        var symlinkResults = new List<(string Target, bool Created, IReadOnlyList<string> Migrated)>();

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
                    var result = await SymlinkManager.EnsureSkillsSymlinkAsync(agentsDir, dir, ct).ConfigureAwait(false);
                    symlinkResults.Add((dir, result.Created, result.Migrated));
                }
            }
        }
        else
        {
            var targets = config.Symlinks?.Targets ?? [];
            foreach (var target in targets)
            {
                var targetDir = Path.Combine(scope.Root, target);
                var result = await SymlinkManager.EnsureSkillsSymlinkAsync(agentsDir, targetDir, ct).ConfigureAwait(false);
                symlinkResults.Add((target, result.Created, result.Migrated));
            }

            var seenParentDirs = new HashSet<string>(targets, StringComparer.Ordinal);
            foreach (var agentId in config.Agents)
            {
                var agent = AgentRegistry.GetAgent(agentId);
                if (agent?.SkillsParentDir is null) continue;
                if (!seenParentDirs.Add(agent.SkillsParentDir)) continue;
                var targetDir = Path.Combine(scope.Root, agent.SkillsParentDir);
                var result = await SymlinkManager.EnsureSkillsSymlinkAsync(agentsDir, targetDir, ct).ConfigureAwait(false);
                symlinkResults.Add((agent.SkillsParentDir, result.Created, result.Migrated));
            }
        }

        // Auto-install declared skills (best-effort)
        if (config.Skills.Count > 0)
        {
            try
            {
                await InstallCommand.RunInstallAsync(new InstallOptions(scope), ct).ConfigureAwait(false);
            }
            catch (TrustException) { throw; }
            catch
            {
                Console.WriteLine("Could not install skills. Run `netagents install` to install them later.");
            }
        }

        PrintSummary(scope, symlinkResults);
    }

    private static void PrintSummary(
        ScopeRoot scope,
        List<(string Target, bool Created, IReadOnlyList<string> Migrated)> symlinks)
    {
        var prefix = scope.Scope == ScopeKind.User ? "~/.agents/" : "";
        Console.WriteLine($"Created {prefix}agents.toml");
        Console.WriteLine($"Created {prefix}{(scope.Scope == ScopeKind.User ? "" : ".agents/")}skills/");
        if (scope.Scope == ScopeKind.Project)
            Console.WriteLine("Created .agents/.gitignore");

        foreach (var s in symlinks)
        {
            if (s.Created)
            {
                var label = $"{s.Target}/skills/";
                var source = scope.Scope == ScopeKind.User ? "~/.agents/skills/" : ".agents/skills/";
                Console.WriteLine($"Created symlink: {label} -> {source}");
            }

            if (s.Migrated.Count > 0)
            {
                var dest = scope.Scope == ScopeKind.User ? "~/.agents/" : ".agents/";
                Console.WriteLine($"Migrated {s.Migrated.Count} skill(s) from {s.Target}/skills/ to {dest}skills/");
            }
        }

        var cmd = scope.Scope == ScopeKind.User ? "netagents --user" : "netagents";
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine($"  1. Add more skills: {cmd} add @anthropics/pdf-processing");
        Console.WriteLine($"  2. Install: {cmd} install");
    }

    public static async Task<string> InstallPostMergeHookAsync(string gitDir, CancellationToken ct = default)
    {
        var hooksDir = Path.Combine(gitDir, "hooks");
        Directory.CreateDirectory(hooksDir);

        var hookPath = Path.Combine(hooksDir, "post-merge");

        if (File.Exists(hookPath))
        {
            var existing = await File.ReadAllTextAsync(hookPath, ct).ConfigureAwait(false);
            if (existing.Contains(PostMergeMarker, StringComparison.Ordinal))
                return "exists";

            await File.WriteAllTextAsync(hookPath, $"{existing.TrimEnd()}\n{PostMergeSnippet}", ct)
                .ConfigureAwait(false);
        }
        else
        {
            await File.WriteAllTextAsync(hookPath, $"#!/bin/sh\n{PostMergeSnippet}", ct).ConfigureAwait(false);
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(hookPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        return "created";
    }

    public static async Task<int> ExecuteAsync(string[] args, bool isUser, CancellationToken ct = default)
    {
        var force = args.Contains("--force");
        var agentsArg = args.SkipWhile(a => a != "--agents").Skip(1).FirstOrDefault();

        ScopeRoot scope;
        if (isUser)
            scope = ScopeResolver.ResolveScope(ScopeKind.User);
        else if (ScopeResolver.IsInsideGitRepo(Path.GetFullPath(".")))
            scope = ScopeResolver.ResolveScope(ScopeKind.Project, Path.GetFullPath("."));
        else
        {
            Console.Error.WriteLine("No project found, using user scope (~/.agents/)");
            scope = ScopeResolver.ResolveScope(ScopeKind.User);
        }

        try
        {
            var agents = agentsArg?.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            await RunInitAsync(new InitOptions(scope, force, agents), ct).ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex) when (ex is InitException or TrustException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
