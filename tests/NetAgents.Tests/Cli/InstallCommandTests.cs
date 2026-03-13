namespace NetAgents.Tests.Cli;

using NetAgents.Cli.Commands;
using NetAgents.Lockfile;
using Utils;
using Xunit;

file sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Directory.CreateDirectory(Path);
    }

    public string Path { get; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());

    public void Dispose()
    {
        if (!Directory.Exists(Path)) return;
        foreach (var info in new DirectoryInfo(Path).EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
            info.Attributes = FileAttributes.Normal;
        Directory.Delete(Path, true);
    }
}

file static class GitHelper
{
    public static async Task<string> CreateRepoWithSkills(string parentDir, CancellationToken ct,
        params string[] skillPaths)
    {
        var repoDir = Path.Combine(parentDir, "repo");
        Directory.CreateDirectory(repoDir);
        await ProcessRunner.RunAsync("git", ["init"], repoDir, ct: ct);
        await ProcessRunner.RunAsync("git", ["config", "user.email", "test@test.com"], repoDir, ct: ct);
        await ProcessRunner.RunAsync("git", ["config", "user.name", "Test"], repoDir, ct: ct);

        foreach (var sp in skillPaths)
        {
            var dir = Path.Combine(repoDir, sp);
            Directory.CreateDirectory(dir);
            var name = Path.GetFileName(sp);
            File.WriteAllText(Path.Combine(dir, "SKILL.md"),
                $"---\nname: {name}\ndescription: Test skill {name}\n---\n\n# {name}\n");
        }

        await ProcessRunner.RunAsync("git", ["add", "."], repoDir, ct: ct);
        await ProcessRunner.RunAsync("git", ["commit", "-m", "initial"], repoDir, ct: ct);
        return repoDir;
    }
}

[Collection("SerialGit")]
public sealed class InstallCommandTests
{
    private static CancellationToken CT => TestContext.Current.CancellationToken;

    private static string SetupProject(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, ".agents", "skills"));
        return root;
    }

    [Fact]
    public async Task InstallsSkillFromGitSource()
    {
        using var tmp = new TempDir();
        var project = Path.Combine(tmp.Path, "project");
        SetupProject(project);
        var repoDir = await GitHelper.CreateRepoWithSkills(tmp.Path, CT, "pdf");
        File.WriteAllText(Path.Combine(project, "agents.toml"),
            $"version = 1\n\n[[skills]]\nname = \"pdf\"\nsource = \"git:{repoDir}\"\n");
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);
            var result = await InstallCommand.RunInstallAsync(new InstallOptions(scope), CT);

            Assert.Contains("pdf", result.Installed);
            Assert.True(File.Exists(Path.Combine(project, ".agents", "skills", "pdf", "SKILL.md")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }

    [Fact]
    public async Task CreatesLockfileAfterInstall()
    {
        using var tmp = new TempDir();
        var project = Path.Combine(tmp.Path, "project");
        SetupProject(project);
        var repoDir = await GitHelper.CreateRepoWithSkills(tmp.Path, CT, "pdf");
        File.WriteAllText(Path.Combine(project, "agents.toml"),
            $"version = 1\n\n[[skills]]\nname = \"pdf\"\nsource = \"git:{repoDir}\"\n");
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);
            await InstallCommand.RunInstallAsync(new InstallOptions(scope), CT);

            var lockfile = await LockfileLoader.LoadAsync(Path.Combine(project, "agents.lock"), CT);
            Assert.NotNull(lockfile);
            Assert.True(lockfile.Skills.ContainsKey("pdf"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }

    [Fact]
    public async Task HandlesEmptySkillsList()
    {
        using var tmp = new TempDir();
        var project = Path.Combine(tmp.Path, "project");
        SetupProject(project);
        File.WriteAllText(Path.Combine(project, "agents.toml"), "version = 1\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);

        var result = await InstallCommand.RunInstallAsync(new InstallOptions(scope), CT);

        Assert.Empty(result.Installed);
    }

    [Fact]
    public async Task FailsWithFrozen_WhenNoLockfile()
    {
        using var tmp = new TempDir();
        var project = Path.Combine(tmp.Path, "project");
        SetupProject(project);
        var repoDir = await GitHelper.CreateRepoWithSkills(tmp.Path, CT, "pdf");
        File.WriteAllText(Path.Combine(project, "agents.toml"),
            $"version = 1\n\n[[skills]]\nname = \"pdf\"\nsource = \"git:{repoDir}\"\n");
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);
            await Assert.ThrowsAsync<InstallException>(() =>
                InstallCommand.RunInstallAsync(new InstallOptions(scope, true), CT));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }

    [Fact]
    public async Task FrozenMode_PassesWhenLockfileMatches()
    {
        using var tmp = new TempDir();
        var project = Path.Combine(tmp.Path, "project");
        SetupProject(project);
        var repoDir = await GitHelper.CreateRepoWithSkills(tmp.Path, CT, "pdf");
        File.WriteAllText(Path.Combine(project, "agents.toml"),
            $"version = 1\n\n[[skills]]\nname = \"pdf\"\nsource = \"git:{repoDir}\"\n");
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);
            await InstallCommand.RunInstallAsync(new InstallOptions(scope), CT);
            var result = await InstallCommand.RunInstallAsync(new InstallOptions(scope, true), CT);

            Assert.Contains("pdf", result.Installed);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }

    [Fact]
    public async Task SkipsCopyForInPlacePathSkill()
    {
        using var tmp = new TempDir();
        var project = Path.Combine(tmp.Path, "project");
        SetupProject(project);
        var skillDir = Path.Combine(project, ".agents", "skills", "local-skill");
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "---\nname: local-skill\ndescription: local\n---\n");
        File.WriteAllText(Path.Combine(project, "agents.toml"),
            "version = 1\n\n[[skills]]\nname = \"local-skill\"\nsource = \"path:.agents/skills/local-skill\"\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);

        var result = await InstallCommand.RunInstallAsync(new InstallOptions(scope), CT);

        Assert.Contains("local-skill", result.Installed);
        var lockfile = await LockfileLoader.LoadAsync(Path.Combine(project, "agents.lock"), CT);
        Assert.NotNull(lockfile);
        Assert.Equal("path:.agents/skills/local-skill", lockfile.Skills["local-skill"].Source);
    }

    [Fact]
    public async Task InstallsAllSkillsFromWildcard()
    {
        using var tmp = new TempDir();
        var project = Path.Combine(tmp.Path, "project");
        SetupProject(project);
        var repoDir = await GitHelper.CreateRepoWithSkills(tmp.Path, CT, "pdf", "skills/review");
        File.WriteAllText(Path.Combine(project, "agents.toml"),
            $"version = 1\n\n[[skills]]\nname = \"*\"\nsource = \"git:{repoDir}\"\n");
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);
            var result = await InstallCommand.RunInstallAsync(new InstallOptions(scope), CT);

            Assert.Contains("pdf", result.Installed);
            Assert.Contains("review", result.Installed);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }

    [Fact]
    public async Task WildcardRespectsExcludeList()
    {
        using var tmp = new TempDir();
        var project = Path.Combine(tmp.Path, "project");
        SetupProject(project);
        var repoDir = await GitHelper.CreateRepoWithSkills(tmp.Path, CT, "pdf", "skills/review");
        File.WriteAllText(Path.Combine(project, "agents.toml"),
            $"version = 1\n\n[[skills]]\nname = \"*\"\nsource = \"git:{repoDir}\"\nexclude = [\"review\"]\n");
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);
            var result = await InstallCommand.RunInstallAsync(new InstallOptions(scope), CT);

            Assert.Contains("pdf", result.Installed);
            Assert.DoesNotContain("review", result.Installed);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }
}
