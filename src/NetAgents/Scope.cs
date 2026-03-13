namespace NetAgents;

using System.Text.RegularExpressions;

public enum ScopeKind
{
    Project,
    User
}

public sealed record ScopeRoot(
    ScopeKind Scope,
    string Root,
    string AgentsDir,
    string ConfigPath,
    string LockPath,
    string SkillsDir);

public static partial class ScopeResolver
{
    [GeneratedRegex(@"^gitdir:\s+(.+)$")]
    private static partial Regex GitdirFilePattern();

    public static ScopeRoot ResolveScope(ScopeKind scope, string? projectRoot = null)
    {
        if (scope == ScopeKind.User)
        {
            var home = Environment.GetEnvironmentVariable("NETAGENTS_HOME")
                       ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agents");
            return new ScopeRoot(
                ScopeKind.User,
                home,
                home,
                Path.Combine(home, "agents.toml"),
                Path.Combine(home, "agents.lock"),
                Path.Combine(home, "skills"));
        }

        var root = projectRoot ?? Directory.GetCurrentDirectory();
        var agentsDir = Path.Combine(root, ".agents");
        return new ScopeRoot(
            ScopeKind.Project,
            root,
            agentsDir,
            Path.Combine(root, "agents.toml"),
            Path.Combine(root, "agents.lock"),
            Path.Combine(agentsDir, "skills"));
    }

    public static bool IsInsideGitRepo(string dir)
    {
        return FindGitDir(dir) is not null;
    }

    public static string? FindGitDir(string dir)
    {
        var current = Path.GetFullPath(dir);

        while (true)
        {
            var gitPath = Path.Combine(current, ".git");

            if (File.Exists(gitPath))
            {
                // .git is a file — submodule or worktree pointer
                var content = File.ReadAllText(gitPath).Trim();
                var match = GitdirFilePattern().Match(content);
                if (!match.Success)
                    return null;

                var target = Path.GetFullPath(Path.Combine(current, match.Groups[1].Value));
                if (!Directory.Exists(target))
                    return null;

                return ResolveCommonGitDir(target);
            }

            if (Directory.Exists(gitPath))
                return gitPath;

            var parent = Path.GetDirectoryName(current);
            if (parent is null || parent == current)
                return null;

            current = parent;
        }
    }

    private static string ResolveCommonGitDir(string gitDir)
    {
        var commondirPath = Path.Combine(gitDir, "commondir");
        if (File.Exists(commondirPath))
        {
            var rel = File.ReadAllText(commondirPath).Trim();
            var common = Path.GetFullPath(Path.Combine(gitDir, rel));
            if (Directory.Exists(common))
                return common;
        }

        return gitDir;
    }

    public static ScopeRoot ResolveDefaultScope(string projectRoot)
    {
        if (File.Exists(Path.Combine(projectRoot, "agents.toml")))
            return ResolveScope(ScopeKind.Project, projectRoot);

        if (!IsInsideGitRepo(projectRoot))
        {
            Console.Error.WriteLine("No project found, using user scope (~/.agents/)");
            return ResolveScope(ScopeKind.User);
        }

        throw new ScopeException(
            "No agents.toml found. Run 'netagents init' to set up this project, or use --user for user scope.");
    }
}

public sealed class ScopeException(string message) : Exception(message);
