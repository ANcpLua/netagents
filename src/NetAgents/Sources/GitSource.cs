using System.Text.RegularExpressions;
using NetAgents.Utils;

namespace NetAgents.Sources;

public sealed class GitException(string message) : Exception(message);

public static partial class GitSource
{
    [GeneratedRegex(@"^https?://(github\.com|gitlab\.com)/(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex HostedRepoPattern();

    /// <summary>
    /// Clone a repo with --depth=1 into the given directory.
    /// If ref is provided, clones that specific ref.
    /// </summary>
    public static async Task CloneAsync(
        string url,
        string dest,
        string? @ref = null,
        CancellationToken ct = default)
    {
        var args = new List<string> { "clone", "--depth=1" };
        if (@ref is not null)
        {
            args.Add("--branch");
            args.Add(@ref);
        }

        args.Add("--");
        args.Add(url);
        args.Add(dest);

        try
        {
            await ProcessRunner.RunAsync("git", [.. args], ct: ct).ConfigureAwait(false);
        }
        catch (ProcessRunnerException ex)
        {
            var stderr = ex.Stderr;
            var sshUrl = ToSshCloneUrl(url);

            if (sshUrl is not null &&
                (stderr.Contains("terminal prompts disabled", StringComparison.OrdinalIgnoreCase) ||
                 stderr.Contains("could not read Username", StringComparison.OrdinalIgnoreCase)))
            {
                throw new GitException(
                    $"Failed to clone {url}: authentication required.\n" +
                    $"Hint: for private repos, use the SSH URL instead:\n" +
                    $"  netagents add {sshUrl}");
            }

            throw new GitException($"Failed to clone {url}: {stderr}");
        }
    }

    /// <summary>
    /// Fetch latest and reset to origin's HEAD. For updating unpinned repos.
    /// </summary>
    public static async Task FetchAndResetAsync(string repoDir, CancellationToken ct = default)
    {
        try
        {
            await ProcessRunner.RunAsync("git", ["fetch", "--depth=1", "--", "origin"], cwd: repoDir, ct: ct)
                .ConfigureAwait(false);
            await ProcessRunner.RunAsync("git", ["reset", "--hard", "FETCH_HEAD"], cwd: repoDir, ct: ct)
                .ConfigureAwait(false);
        }
        catch (ProcessRunnerException ex)
        {
            throw new GitException($"Failed to update {repoDir}: {ex.Stderr}");
        }
    }

    /// <summary>
    /// Fetch a specific ref and checkout.
    /// </summary>
    public static async Task FetchRefAsync(string repoDir, string @ref, CancellationToken ct = default)
    {
        try
        {
            await ProcessRunner.RunAsync("git", ["fetch", "--depth=1", "--", "origin", @ref], cwd: repoDir, ct: ct)
                .ConfigureAwait(false);
            await ProcessRunner.RunAsync("git", ["checkout", "FETCH_HEAD"], cwd: repoDir, ct: ct)
                .ConfigureAwait(false);
        }
        catch (ProcessRunnerException ex)
        {
            throw new GitException($"Failed to fetch ref {@ref} in {repoDir}: {ex.Stderr}");
        }
    }

    /// <summary>
    /// Get the current HEAD commit SHA (full 40 chars).
    /// </summary>
    public static async Task<string> HeadCommitAsync(string repoDir, CancellationToken ct = default)
    {
        var result = await ProcessRunner.RunAsync("git", ["rev-parse", "HEAD"], cwd: repoDir, ct: ct)
            .ConfigureAwait(false);
        return result.Stdout.Trim();
    }

    /// <summary>
    /// Check if a directory is a git repository.
    /// </summary>
    public static bool IsGitRepo(string dir) => Directory.Exists(Path.Combine(dir, ".git"));

    private static string? ToSshCloneUrl(string url)
    {
        var match = HostedRepoPattern().Match(url);
        if (!match.Success)
            return null;

        var host = match.Groups[1].Value.ToLowerInvariant();
        var rawPath = match.Groups[2].Value.TrimEnd('/');

        return $"git@{host}:{(rawPath.EndsWith(".git", StringComparison.Ordinal) ? rawPath : $"{rawPath}.git")}";
    }
}
