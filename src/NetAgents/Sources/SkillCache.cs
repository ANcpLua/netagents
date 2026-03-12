namespace NetAgents.Sources;

public sealed record CacheResult(string RepoDir, string Commit);

public static class SkillCache
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    private static string DefaultStateDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "netagents");

    /// <summary>
    /// Get or populate the global cache for a git source.
    /// Cache layout: ~/.local/netagents/{cacheKey}/ — TTL-refreshed shallow clone
    /// </summary>
    public static async Task<CacheResult> EnsureCachedAsync(
        string url,
        string cacheKey,
        string? @ref = null,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
    {
        var stateDir = Environment.GetEnvironmentVariable("NETAGENTS_STATE_DIR") ?? DefaultStateDir;
        var effectiveTtl = ttl ?? DefaultTtl;
        // Ensure cacheKey is relative so Path.Combine doesn't discard the stateDir
        var safeCacheKey = cacheKey.TrimStart(Path.DirectorySeparatorChar).TrimStart(Path.AltDirectorySeparatorChar);
        var repoDir = Path.Combine(stateDir, safeCacheKey);

        if (GitSource.IsGitRepo(repoDir))
        {
            // Always fetch when a specific ref is requested — the cached checkout
            // may point at a different ref regardless of staleness.
            var needsRefresh = @ref is not null || IsStale(repoDir, effectiveTtl);
            if (needsRefresh)
            {
                if (@ref is not null)
                    await GitSource.FetchRefAsync(repoDir, @ref, ct).ConfigureAwait(false);
                else
                    await GitSource.FetchAndResetAsync(repoDir, ct).ConfigureAwait(false);
            }

            var commit = await GitSource.HeadCommitAsync(repoDir, ct).ConfigureAwait(false);
            return new CacheResult(repoDir, commit);
        }

        // Not cached yet — clone
        var parentDir = Path.GetDirectoryName(repoDir);
        if (parentDir is not null)
            Directory.CreateDirectory(parentDir);

        await GitSource.CloneAsync(url, repoDir, @ref, ct).ConfigureAwait(false);
        var clonedCommit = await GitSource.HeadCommitAsync(repoDir, ct).ConfigureAwait(false);
        return new CacheResult(repoDir, clonedCommit);
    }

    private static bool IsStale(string repoDir, TimeSpan ttl)
    {
        var fetchHeadPath = Path.Combine(repoDir, ".git", "FETCH_HEAD");
        try
        {
            var lastWrite = File.GetLastWriteTimeUtc(fetchHeadPath);
            return TimeProvider.System.GetUtcNow() - lastWrite > ttl;
        }
        catch (FileNotFoundException)
        {
            // No FETCH_HEAD yet — consider stale
            return true;
        }
    }
}
