namespace NetAgents.Sources;

public sealed class LocalSourceException(string message) : Exception(message);

public static class LocalSource
{
    /// <summary>
    ///     Resolve a path: source to an absolute directory.
    ///     The path is relative to the project root.
    /// </summary>
    public static async Task<string> ResolveLocalSourceAsync(
        string projectRoot,
        string relativePath,
        CancellationToken ct = default)
    {
        _ = ct; // reserved for future use

        var absRoot = Path.GetFullPath(projectRoot);
        var absPath = Path.GetFullPath(Path.Combine(projectRoot, relativePath));

        // Prevent path traversal outside the project root
        if (!absPath.StartsWith(absRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
            !string.Equals(absPath, absRoot, StringComparison.Ordinal))
            throw new LocalSourceException(
                $"Local source \"{relativePath}\" resolves outside project root");

        var exists = await Task.Run(() => Directory.Exists(absPath), ct).ConfigureAwait(false);
        if (!exists)
            throw new LocalSourceException($"Local source not found: {absPath}");

        var isDirectory = await Task.Run(() =>
        {
            var attrs = File.GetAttributes(absPath);
            return attrs.HasFlag(FileAttributes.Directory);
        }, ct).ConfigureAwait(false);

        if (!isDirectory)
            throw new LocalSourceException($"Local source is not a directory: {absPath}");

        return absPath;
    }
}
