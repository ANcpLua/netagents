namespace NetAgents.Utils;

public static class FileSystem
{
    /// <summary>
    /// Recursively deletes a directory, clearing read-only attributes first.
    /// Git marks .git/objects/* as read-only on Windows, which causes
    /// <see cref="Directory.Delete(string, bool)"/> to throw
    /// <see cref="UnauthorizedAccessException"/>.
    /// </summary>
    public static void ForceDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            var attrs = File.GetAttributes(file);
            if ((attrs & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(file, attrs & ~FileAttributes.ReadOnly);
        }

        Directory.Delete(path, true);
    }

    public static async Task CopyDirectoryAsync(string src, string dest)
    {
        if (Directory.Exists(dest))
            ForceDeleteDirectory(dest);

        await CopyRecursiveAsync(src, dest).ConfigureAwait(false);
    }

    private static async Task CopyRecursiveAsync(string src, string dest)
    {
        Directory.CreateDirectory(dest);

        foreach (var file in Directory.EnumerateFiles(src))
        {
            var destFile = Path.Combine(dest, Path.GetFileName(file));
            await using var sourceStream =
                new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            await using var destStream =
                new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            await sourceStream.CopyToAsync(destStream).ConfigureAwait(false);
        }

        foreach (var subDir in Directory.EnumerateDirectories(src))
        {
            var destSubDir = Path.Combine(dest, Path.GetFileName(subDir));
            await CopyRecursiveAsync(subDir, destSubDir).ConfigureAwait(false);
        }
    }
}
