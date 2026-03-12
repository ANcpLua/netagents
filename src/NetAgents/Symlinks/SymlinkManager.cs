using NetAgents.Utils;

namespace NetAgents.Symlinks;

public sealed class SymlinkException(string message) : Exception(message);

public sealed record SymlinkResult(bool Created, IReadOnlyList<string> Migrated);

public sealed record SymlinkIssue(string Target, string Issue);

public static class SymlinkManager
{
    /// <summary>
    /// Ensure &lt;targetDir&gt;/skills/ is a symlink pointing to &lt;agentsDir&gt;/skills/.
    /// Creates the parent directory if it doesn't exist.
    /// </summary>
    public static async Task<SymlinkResult> EnsureSkillsSymlinkAsync(
        string agentsDir,
        string targetDir,
        CancellationToken ct = default)
    {
        var skillsSource = Path.Combine(agentsDir, "skills");
        var skillsLink = Path.Combine(targetDir, "skills");
        var relativeTarget = Path.GetRelativePath(targetDir, skillsSource);

        // Ensure parent directory exists
        Directory.CreateDirectory(targetDir);

        // Check if skills path already exists
        FileSystemInfo? info = null;
        try
        {
            info = File.GetAttributes(skillsLink).HasFlag(FileAttributes.ReparsePoint)
                ? new FileInfo(skillsLink)
                : Directory.Exists(skillsLink)
                    ? new DirectoryInfo(skillsLink)
                    : File.Exists(skillsLink)
                        ? new FileInfo(skillsLink)
                        : null;
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }

        if (info is null)
        {
            // Doesn't exist, create symlink
            File.CreateSymbolicLink(skillsLink, relativeTarget);
            return new SymlinkResult(true, []);
        }

        // Already a symlink - check if it points to the right place
        if (info.LinkTarget is not null)
        {
            if (info.LinkTarget == relativeTarget)
                return new SymlinkResult(false, []);

            // Wrong target, replace
            File.Delete(skillsLink);
            File.CreateSymbolicLink(skillsLink, relativeTarget);
            return new SymlinkResult(true, []);
        }

        // Real directory - migrate contents then replace with symlink
        if (Directory.Exists(skillsLink))
        {
            var migrated = MigrateDirectory(skillsLink, skillsSource);
            await RemoveFromGitIndexAsync(targetDir, "skills", ct).ConfigureAwait(false);
            Directory.Delete(skillsLink, recursive: true);
            File.CreateSymbolicLink(skillsLink, relativeTarget);
            return new SymlinkResult(true, migrated);
        }

        throw new SymlinkException($"{skillsLink} exists but is not a directory or symlink");
    }

    private static List<string> MigrateDirectory(string from, string to)
    {
        var migrated = new List<string>();

        foreach (var entry in new DirectoryInfo(from).EnumerateFileSystemInfos())
        {
            var destPath = Path.Combine(to, entry.Name);

            // Skip if destination already exists
            if (Path.Exists(destPath))
                continue;

            Directory.Move(Path.Combine(from, entry.Name), destPath);
            migrated.Add(entry.Name);
        }

        return migrated;
    }

    /// <summary>
    /// Best-effort removal of tracked files from git's index.
    /// Prevents "beyond a symbolic link" errors when a tracked directory
    /// is replaced by a symlink.
    /// </summary>
    private static async Task RemoveFromGitIndexAsync(string cwd, string path, CancellationToken ct)
    {
        try
        {
            await ProcessRunner.RunAsync("git", ["rm", "-r", "--cached", "--ignore-unmatch", path], cwd: cwd, ct: ct)
                .ConfigureAwait(false);
        }
        catch
        {
            // Silently ignore: not a git repo, git not installed, etc.
        }
    }

    /// <summary>
    /// Verify all configured symlinks are correct.
    /// Returns a list of issues found.
    /// </summary>
    public static Task<IReadOnlyList<SymlinkIssue>> VerifySymlinksAsync(
        string agentsDir,
        IReadOnlyList<string> targets,
        CancellationToken ct = default)
    {
        _ = ct; // reserved for future async operations
        var issues = new List<SymlinkIssue>();
        var skillsSource = Path.Combine(agentsDir, "skills");

        foreach (var target in targets)
        {
            var skillsLink = Path.Combine(target, "skills");
            var relativeTarget = Path.GetRelativePath(target, skillsSource);

            // Check existence first — symlinks may not resolve, so check both FileInfo and Directory
            var fi = new FileInfo(skillsLink);
            if (!fi.Exists && !Directory.Exists(skillsLink) && fi.LinkTarget is null)
            {
                issues.Add(new SymlinkIssue(target, $"{skillsLink} does not exist"));
                continue;
            }

            // Resolve link target from either FileInfo or DirectoryInfo
            var linkTarget = fi.LinkTarget ?? new DirectoryInfo(skillsLink).LinkTarget;

            if (linkTarget is null)
            {
                issues.Add(new SymlinkIssue(target, $"{skillsLink} is not a symlink"));
                continue;
            }

            if (linkTarget != relativeTarget)
            {
                issues.Add(new SymlinkIssue(target, $"{skillsLink} points to {linkTarget}, expected {relativeTarget}"));
            }
        }

        return Task.FromResult<IReadOnlyList<SymlinkIssue>>(issues);
    }
}
