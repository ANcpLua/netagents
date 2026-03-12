namespace NetAgents.Utils;

public static class FileSystem
{
    public static async Task CopyDirectoryAsync(string src, string dest)
    {
        if (Directory.Exists(dest))
            Directory.Delete(dest, recursive: true);

        await CopyRecursiveAsync(src, dest).ConfigureAwait(false);
    }

    private static async Task CopyRecursiveAsync(string src, string dest)
    {
        Directory.CreateDirectory(dest);

        foreach (var file in Directory.EnumerateFiles(src))
        {
            var destFile = Path.Combine(dest, Path.GetFileName(file));
            await using var sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
            await using var destStream = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
            await sourceStream.CopyToAsync(destStream).ConfigureAwait(false);
        }

        foreach (var subDir in Directory.EnumerateDirectories(src))
        {
            var destSubDir = Path.Combine(dest, Path.GetFileName(subDir));
            await CopyRecursiveAsync(subDir, destSubDir).ConfigureAwait(false);
        }
    }
}
