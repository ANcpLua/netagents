using NetAgents.Agents;
using NetAgents.Config;
using NetAgents.Gitignore;
using NetAgents.Lockfile;
using NetAgents.Symlinks;
using NetAgents.Utils;
using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace NetAgents.Cli.Commands;

public sealed record DoctorCheck(string Name, string Status, string Message, Func<Task>? Fix = null);

public sealed record DoctorOptions(ScopeRoot Scope, bool Fix = false);

public sealed record DoctorResult(IReadOnlyList<DoctorCheck> Checks, int Fixed);

public static class DoctorCommand
{
    private static readonly string[] GeneratedFiles = ["agents.lock", ".agents/.gitignore"];

    public static async Task<DoctorResult> RunDoctorAsync(DoctorOptions opts, CancellationToken ct = default)
    {
        var (scope, fix) = (opts.Scope, opts.Fix);
        var checks = new List<DoctorCheck>();
        var fixed_ = 0;

        // 1. Check agents.toml exists
        if (!File.Exists(scope.ConfigPath))
        {
            checks.Add(new DoctorCheck("agents.toml", "error",
                "agents.toml not found. Run 'netagents init' to create one."));
            return new DoctorResult(checks, fixed_);
        }

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, ct).ConfigureAwait(false);

        // 2. Legacy fields in agents.toml
        var rawToml = await File.ReadAllTextAsync(scope.ConfigPath, ct).ConfigureAwait(false);
        var configDoc = SyntaxParser.Parse(rawToml);
        var hasKey = (DocumentSyntax doc, string key) =>
            doc.KeyValues.Any(kv => kv.Key?.ToString()?.Trim() == key);

        if (hasKey(configDoc, "pin"))
        {
            checks.Add(new DoctorCheck("legacy pin field", "warn",
                "agents.toml contains 'pin' which is no longer used in v1. Remove it.",
                async () =>
                {
                    var content = await File.ReadAllTextAsync(scope.ConfigPath, ct).ConfigureAwait(false);
                    var cleaned = string.Join("\n",
                        content.Split('\n').Where(line => !System.Text.RegularExpressions.Regex.IsMatch(line, @"^\s*pin\s*=")));
                    await File.WriteAllTextAsync(scope.ConfigPath, cleaned, ct).ConfigureAwait(false);
                }));
        }

        if (hasKey(configDoc, "gitignore"))
        {
            checks.Add(new DoctorCheck("legacy gitignore field", "warn",
                "agents.toml contains 'gitignore' which is no longer used in v1. Gitignore is always managed. Remove it.",
                async () =>
                {
                    var content = await File.ReadAllTextAsync(scope.ConfigPath, ct).ConfigureAwait(false);
                    var cleaned = string.Join("\n",
                        content.Split('\n').Where(line =>
                        {
                            var trimmed = line.Trim();
                            return !System.Text.RegularExpressions.Regex.IsMatch(line, @"^\s*gitignore\s*=") &&
                                   !trimmed.StartsWith("# Managed skills are gitignored", StringComparison.Ordinal) &&
                                   !trimmed.StartsWith("# Check skills into git", StringComparison.Ordinal);
                        }));
                    await File.WriteAllTextAsync(scope.ConfigPath, cleaned, ct).ConfigureAwait(false);
                }));
        }

        // 3. Legacy fields in agents.lock
        var lockfile = await LockfileLoader.LoadAsync(scope.LockPath, ct).ConfigureAwait(false);
        if (lockfile is not null && File.Exists(scope.LockPath))
        {
            var rawLockToml = await File.ReadAllTextAsync(scope.LockPath, ct).ConfigureAwait(false);
            var lockDoc = SyntaxParser.Parse(rawLockToml);
            var skillsTable = lockDoc.Tables.FirstOrDefault(t =>
                t.Name?.ToString()?.Trim().StartsWith("skills.", StringComparison.Ordinal) == true);
            if (skillsTable is not null)
            {
                var hasLegacy = lockDoc.Tables
                    .Where(t => t.Name?.ToString()?.Trim().StartsWith("skills.", StringComparison.Ordinal) == true)
                    .Any(t => t.Items.Any(kv => kv.Key?.ToString()?.Trim() is "commit" or "integrity"));
                if (hasLegacy)
                {
                    checks.Add(new DoctorCheck("legacy lockfile fields", "warn",
                        "agents.lock contains 'commit' or 'integrity' fields from v0. These are no longer used.",
                        async () => await LockfileWriter.WriteAsync(scope.LockPath, lockfile, ct).ConfigureAwait(false)));
                }
            }
        }

        // 4. Root .gitignore health check
        if (scope.Scope == ScopeKind.Project)
        {
            var missing = await GitignoreWriter.CheckRootGitignoreEntriesAsync(scope.Root, ct).ConfigureAwait(false);
            if (missing.Count > 0)
            {
                checks.Add(new DoctorCheck("root .gitignore", "error",
                    $"Missing from .gitignore: {string.Join(", ", missing)}. These files should not be committed.",
                    async () => await GitignoreWriter.EnsureRootGitignoreEntriesAsync(scope.Root, ct).ConfigureAwait(false)));
            }
            else
            {
                checks.Add(new DoctorCheck("root .gitignore", "ok", "Root .gitignore is configured correctly."));
            }
        }

        // 5. Tracked generated files
        if (scope.Scope == ScopeKind.Project)
        {
            var trackedFiles = await FindTrackedGeneratedFilesAsync(scope.Root, ct).ConfigureAwait(false);
            if (trackedFiles.Count > 0)
            {
                checks.Add(new DoctorCheck("tracked generated files", "warn",
                    $"Generated files checked into git: {string.Join(", ", trackedFiles)}. Remove them with 'git rm --cached'.",
                    async () => await ProcessRunner.RunAsync("git", ["rm", "--cached", .. trackedFiles], cwd: scope.Root, ct: ct).ConfigureAwait(false)));
            }
        }

        // 6. .agents/.gitignore exists
        if (scope.Scope == ScopeKind.Project)
        {
            if (File.Exists(Path.Combine(scope.AgentsDir, ".gitignore")))
            {
                checks.Add(new DoctorCheck(".agents/.gitignore", "ok", ".agents/.gitignore exists."));
            }
            else
            {
                var managedNames = GetManagedSkillNames(config, lockfile);
                checks.Add(new DoctorCheck(".agents/.gitignore", "warn",
                    ".agents/.gitignore is missing. Run 'netagents install' or 'netagents sync' to regenerate.",
                    async () => await GitignoreWriter.WriteAgentsGitignoreAsync(scope.AgentsDir, managedNames, ct).ConfigureAwait(false)));
            }
        }

        // 7. Skills directory exists
        if (Directory.Exists(scope.SkillsDir))
            checks.Add(new DoctorCheck("skills directory", "ok", "Skills directory exists."));
        else
            checks.Add(new DoctorCheck("skills directory", "warn",
                ".agents/skills/ directory is missing. Run 'netagents install' to create it."));

        // 8. Declared skills are installed
        var declaredNames = GetDeclaredSkillNames(config, lockfile);
        var missingSkills = declaredNames.Where(name => !Directory.Exists(Path.Combine(scope.SkillsDir, name))).ToList();
        if (missingSkills.Count > 0)
            checks.Add(new DoctorCheck("installed skills", "error",
                $"{missingSkills.Count} skill(s) not installed: {string.Join(", ", missingSkills)}. Run 'netagents install'."));
        else if (declaredNames.Count > 0)
            checks.Add(new DoctorCheck("installed skills", "ok", $"All {declaredNames.Count} declared skill(s) installed."));
        else
            checks.Add(new DoctorCheck("installed skills", "ok", "No skills declared."));

        // 9. Symlinks
        if (scope.Scope == ScopeKind.Project && Directory.Exists(scope.AgentsDir))
        {
            var targets = new List<string>();
            var seenDirs = new HashSet<string>(StringComparer.Ordinal);

            foreach (var target in config.Symlinks?.Targets ?? [])
            {
                seenDirs.Add(target);
                targets.Add(Path.Combine(scope.Root, target));
            }

            foreach (var agentId in config.Agents)
            {
                var agent = AgentRegistry.GetAgent(agentId);
                if (agent?.SkillsParentDir is null || !seenDirs.Add(agent.SkillsParentDir)) continue;
                targets.Add(Path.Combine(scope.Root, agent.SkillsParentDir));
            }

            if (targets.Count > 0)
            {
                var symlinkIssues = await SymlinkManager.VerifySymlinksAsync(scope.AgentsDir, targets, ct)
                    .ConfigureAwait(false);
                checks.Add(symlinkIssues.Count > 0
                    ? new DoctorCheck("symlinks", "warn",
                        $"{symlinkIssues.Count} symlink(s) broken or missing. Run 'netagents sync' to repair.")
                    : new DoctorCheck("symlinks", "ok", "All symlinks intact."));
            }
        }

        // Apply fixes
        if (fix)
        {
            foreach (var check in checks)
            {
                if (check.Status != "ok" && check.Fix is not null)
                {
                    await check.Fix().ConfigureAwait(false);
                    fixed_++;
                }
            }
        }

        return new DoctorResult(checks, fixed_);
    }

    private static async Task<IReadOnlyList<string>> FindTrackedGeneratedFilesAsync(string root, CancellationToken ct)
    {
        var tracked = new List<string>();
        foreach (var file in GeneratedFiles)
        {
            try
            {
                var result = await ProcessRunner.RunAsync("git", ["ls-files", file], cwd: root, ct: ct)
                    .ConfigureAwait(false);
                if (result.Stdout.Trim().Length > 0)
                    tracked.Add(file);
            }
            catch (ProcessRunnerException)
            {
                // Not a git repo or git not available
            }
        }
        return tracked;
    }

    private static List<string> GetDeclaredSkillNames(AgentsConfig config, LockfileData? lockfile)
    {
        var names = new HashSet<string>(
            config.Skills.OfType<RegularSkillDependency>().Select(s => s.Name), StringComparer.Ordinal);
        if (lockfile is not null)
            foreach (var name in lockfile.Skills.Keys)
                names.Add(name);
        return [.. names];
    }

    private static List<string> GetManagedSkillNames(AgentsConfig config, LockfileData? lockfile)
    {
        var allNames = lockfile is not null
            ? lockfile.Skills.Keys.ToList()
            : config.Skills.OfType<RegularSkillDependency>().Select(s => s.Name).ToList();
        return allNames.Where(name =>
        {
            var dep = config.Skills.OfType<RegularSkillDependency>().FirstOrDefault(s => s.Name == name);
            if (dep is null) return true;
            return !dep.Source.StartsWith("path:.agents/skills/", StringComparison.Ordinal) &&
                   !dep.Source.StartsWith("path:skills/", StringComparison.Ordinal);
        }).ToList();
    }

    public static async Task<int> ExecuteAsync(string[] args, bool isUser, CancellationToken ct = default)
    {
        var doFix = args.Contains("--fix");

        ScopeRoot scope;
        try
        {
            scope = isUser
                ? ScopeResolver.ResolveScope(ScopeKind.User)
                : ScopeResolver.ResolveDefaultScope(Path.GetFullPath("."));
        }
        catch (ScopeException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var result = await RunDoctorAsync(new DoctorOptions(scope, doFix), ct).ConfigureAwait(false);
        var hasIssues = result.Checks.Any(c => c.Status != "ok");

        foreach (var check in result.Checks)
        {
            var icon = check.Status switch { "ok" => "+", "warn" => "!", _ => "x" };
            Console.WriteLine($"  {icon} {check.Message}");
        }

        var unfixable = result.Checks.Where(c => c.Status != "ok" && c.Fix is null).ToList();

        if (result.Fixed > 0)
        {
            Console.WriteLine($"\nFixed {result.Fixed} issue(s).");
            if (unfixable.Count > 0)
                Console.WriteLine($"{unfixable.Count} issue(s) require manual action.");
        }
        else if (hasIssues && !doFix)
        {
            var fixable = result.Checks.Count(c => c.Status != "ok" && c.Fix is not null);
            if (fixable > 0)
                Console.WriteLine($"\nRun 'netagents doctor --fix' to auto-fix {fixable} issue(s).");
        }
        else if (!hasIssues)
        {
            Console.WriteLine("\nAll checks passed.");
        }

        return hasIssues && (!doFix || unfixable.Count > 0) ? 1 : 0;
    }
}
