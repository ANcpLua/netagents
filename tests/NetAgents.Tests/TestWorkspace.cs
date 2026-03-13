namespace NetAgents.Tests;

internal static class TestWorkspace
{
    public static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        foreach (var info in new DirectoryInfo(path).EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
            info.Attributes = FileAttributes.Normal;

        new DirectoryInfo(path).Attributes = FileAttributes.Normal;
        Directory.Delete(path, true);
    }

    public static string ToGitSource(string repoDir)
    {
        return $"git:{new Uri(Path.GetFullPath(repoDir)).AbsoluteUri}";
    }
}
