namespace NetAgents.Cli.Commands;

using Config;
using Gitignore;
using Lockfile;
using Skills;

public sealed class RemoveException(string message) : Exception(message);

public sealed record RemoveOptions(ScopeRoot Scope, string SkillName);

public sealed record RemoveResult(
    string SkillName,
    bool Removed,
    bool IsWildcard,
    string? WildcardSource,
    string? Hint);

public static class RemoveCommand
{
    public static async Task<RemoveResult> RunRemoveAsync(RemoveOptions opts, CancellationToken ct = default)
    {
        var (scope, skillName) = (opts.Scope, opts.SkillName);
        var (configPath, lockPath, skillsDir) = (scope.ConfigPath, scope.LockPath, scope.SkillsDir);
        var skillDir = Path.Combine(skillsDir, skillName);

        var config = await ConfigLoader.LoadAsync(configPath, ct).ConfigureAwait(false);

        // Check if skill is an explicit entry
        var explicitDep = config.Skills.OfType<RegularSkillDependency>().FirstOrDefault(s => s.Name == skillName);
        if (explicitDep is not null)
        {
            await ConfigWriter.RemoveSkillFromConfigAsync(configPath, skillName, ct).ConfigureAwait(false);
            if (Directory.Exists(skillDir))
                Directory.Delete(skillDir, true);

            var lockfile = await LockfileLoader.LoadAsync(lockPath, ct).ConfigureAwait(false);
            if (lockfile is not null)
            {
                lockfile.Skills.Remove(skillName);
                await LockfileWriter.WriteAsync(lockPath, lockfile, ct).ConfigureAwait(false);
            }

            if (scope.Scope == ScopeKind.Project)
            {
                var updatedConfig = await ConfigLoader.LoadAsync(configPath, ct).ConfigureAwait(false);
                var updatedLock = await LockfileLoader.LoadAsync(lockPath, ct).ConfigureAwait(false);
                var allNames = updatedLock is not null ? updatedLock.Skills.Keys.ToList() : new List<string>();
                var managedNames = allNames.Where(name =>
                {
                    var dep = updatedConfig.Skills.OfType<RegularSkillDependency>().FirstOrDefault(s => s.Name == name);
                    if (dep is null) return true; // wildcard-sourced
                    return !dep.Source.StartsWith("path:.agents/skills/", StringComparison.Ordinal) &&
                           !dep.Source.StartsWith("path:skills/", StringComparison.Ordinal);
                }).ToList();
                await GitignoreWriter.WriteAgentsGitignoreAsync(scope.AgentsDir, managedNames, ct)
                    .ConfigureAwait(false);
            }

            return new RemoveResult(skillName, true, false, null, null);
        }

        // Check if skill is from a wildcard entry (via lockfile source matching)
        var lockfileForWildcard = await LockfileLoader.LoadAsync(lockPath, ct).ConfigureAwait(false);
        if (lockfileForWildcard?.Skills.TryGetValue(skillName, out var locked) == true)
        {
            var wildcardDep = config.Skills.OfType<WildcardSkillDependency>()
                .FirstOrDefault(s => SkillResolver.SourcesMatch(s.Source, locked.Source));
            if (wildcardDep is not null)
                return new RemoveResult(skillName, false, true,
                    locked.Source,
                    $"Skill \"{skillName}\" is provided by wildcard entry for \"{locked.Source}\". Add to exclude list instead.");
        }

        throw new RemoveException($"Skill \"{skillName}\" not found in agents.toml.");
    }

    public static async Task<int> ExecuteAsync(string[] args, bool isUser, CancellationToken ct = default)
    {
        var skillName = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (skillName is null)
        {
            Console.Error.WriteLine("Usage: netagents remove <name>");
            return 1;
        }

        try
        {
            var scope = isUser
                ? ScopeResolver.ResolveScope(ScopeKind.User)
                : ScopeResolver.ResolveDefaultScope(Path.GetFullPath("."));
            await EnsureUserScope.EnsureUserScopeBootstrappedAsync(scope, ct).ConfigureAwait(false);
            var result = await RunRemoveAsync(new RemoveOptions(scope, skillName), ct).ConfigureAwait(false);
            if (result.IsWildcard)
            {
                Console.WriteLine(result.Hint);
                Console.WriteLine("Use 'netagents remove' interactively to add to exclude list.");
            }
            else
            {
                Console.WriteLine($"Removed skill: {skillName}");
            }

            return 0;
        }
        catch (Exception ex) when (ex is ScopeException or RemoveException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
