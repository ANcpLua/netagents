namespace NetAgents.Cli.Commands;

using System.Text.Json;
using Config;
using Lockfile;
using Skills;

public sealed record SkillStatus(string Name, string Source, string Status, string? Wildcard = null);

public sealed record ListOptions(ScopeRoot Scope, bool Json = false);

public static class ListCommand
{
    public static async Task<IReadOnlyList<SkillStatus>> RunListAsync(ListOptions opts, CancellationToken ct = default)
    {
        var (configPath, lockPath, skillsDir) = (opts.Scope.ConfigPath, opts.Scope.LockPath, opts.Scope.SkillsDir);

        var config = await ConfigLoader.LoadAsync(configPath, ct).ConfigureAwait(false);
        var lockfile = await LockfileLoader.LoadAsync(lockPath, ct).ConfigureAwait(false);

        var regularDeps = config.Skills.OfType<RegularSkillDependency>().ToList();
        var wildcardDeps = config.Skills.OfType<WildcardSkillDependency>().ToList();
        var explicitNames = new HashSet<string>(regularDeps.Select(d => d.Name), StringComparer.Ordinal);

        var skillEntries = new Dictionary<string, (string Source, string? Wildcard)>(StringComparer.Ordinal);
        foreach (var dep in regularDeps)
            skillEntries[dep.Name] = (dep.Source, null);

        if (lockfile is not null)
            foreach (var wDep in wildcardDeps)
            {
                var excludeSet = new HashSet<string>(wDep.Exclude, StringComparer.Ordinal);
                foreach (var (name, locked) in lockfile.Skills)
                {
                    if (!SkillResolver.SourcesMatch(locked.Source, wDep.Source)) continue;
                    if (explicitNames.Contains(name)) continue;
                    if (excludeSet.Contains(name)) continue;
                    skillEntries.TryAdd(name, (wDep.Source, wDep.Source));
                }
            }

        var results = new List<SkillStatus>();
        foreach (var name in skillEntries.Keys.Order())
        {
            var (source, wildcard) = skillEntries[name];
            var installed = Path.Combine(skillsDir, name);

            if (!Directory.Exists(installed))
            {
                results.Add(new SkillStatus(name, source, "missing", wildcard));
                continue;
            }

            var locked = lockfile?.Skills.GetValueOrDefault(name);
            results.Add(locked is null
                ? new SkillStatus(name, source, "unlocked", wildcard)
                : new SkillStatus(name, source, "ok", wildcard));
        }

        return results;
    }

    public static async Task<int> ExecuteAsync(string[] args, bool isUser, CancellationToken ct = default)
    {
        var json = args.Contains("--json");

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

        var results = await RunListAsync(new ListOptions(scope, json), ct).ConfigureAwait(false);

        if (results.Count == 0)
        {
            Console.WriteLine("No skills declared in agents.toml.");
            return 0;
        }

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(results,
                NetAgentsJsonContext.Default.IReadOnlyListSkillStatus));
            return 0;
        }

        Console.WriteLine("Skills:");
        foreach (var s in results)
        {
            var wildcard = s.Wildcard is not null ? " (* wildcard)" : "";
            var status = s.Status switch
            {
                "ok" => $"  + {s.Name}  {s.Source}{wildcard}",
                "missing" => $"  x {s.Name}  {s.Source}{wildcard}  not installed",
                "unlocked" => $"  ? {s.Name}  {s.Source}{wildcard}  not in lockfile",
                _ => $"  {s.Name}  {s.Source}"
            };
            Console.WriteLine(status);
        }

        return 0;
    }
}
