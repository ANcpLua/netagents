namespace NetAgents.Tests.Cli;

using AwesomeAssertions;
using NetAgents.Tests;
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
        TestWorkspace.DeleteDirectory(Path);
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
        return TestWorkspace.ToGitSource(repoDir);
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
            $"version = 1\n\n[[skills]]\nname = \"pdf\"\nsource = \"{repoDir}\"\n");
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);
            var result = await InstallCommand.RunInstallAsync(new InstallOptions(scope), CT);

            result.Installed.Should().Contain("pdf");
            File.Exists(Path.Combine(project, ".agents", "skills", "pdf", "SKILL.md")).Should().BeTrue();
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
            $"version = 1\n\n[[skills]]\nname = \"pdf\"\nsource = \"{repoDir}\"\n");
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);
            await InstallCommand.RunInstallAsync(new InstallOptions(scope), CT);

            var lockfile = await LockfileLoader.LoadAsync(Path.Combine(project, "agents.lock"), CT);
            lockfile.Should().NotBeNull();
            lockfile!.Skills.ContainsKey("pdf").Should().BeTrue();
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

        result.Installed.Should().BeEmpty();
    }

    [Fact]
    public async Task FailsWithFrozen_WhenNoLockfile()
    {
        using var tmp = new TempDir();
        var project = Path.Combine(tmp.Path, "project");
        SetupProject(project);
        var repoDir = await GitHelper.CreateRepoWithSkills(tmp.Path, CT, "pdf");
        File.WriteAllText(Path.Combine(project, "agents.toml"),
            $"version = 1\n\n[[skills]]\nname = \"pdf\"\nsource = \"{repoDir}\"\n");
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
            $"version = 1\n\n[[skills]]\nname = \"pdf\"\nsource = \"{repoDir}\"\n");
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);
            await InstallCommand.RunInstallAsync(new InstallOptions(scope), CT);
            var result = await InstallCommand.RunInstallAsync(new InstallOptions(scope, true), CT);

            result.Installed.Should().Contain("pdf");
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

        result.Installed.Should().Contain("local-skill");
        var lockfile = await LockfileLoader.LoadAsync(Path.Combine(project, "agents.lock"), CT);
        lockfile.Should().NotBeNull();
        lockfile!.Skills["local-skill"].Source.Should().Be("path:.agents/skills/local-skill");
    }

    [Fact]
    public async Task InstallsAllSkillsFromWildcard()
    {
        using var tmp = new TempDir();
        var project = Path.Combine(tmp.Path, "project");
        SetupProject(project);
        var repoDir = await GitHelper.CreateRepoWithSkills(tmp.Path, CT, "pdf", "skills/review");
        File.WriteAllText(Path.Combine(project, "agents.toml"),
            $"version = 1\n\n[[skills]]\nname = \"*\"\nsource = \"{repoDir}\"\n");
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);
            var result = await InstallCommand.RunInstallAsync(new InstallOptions(scope), CT);

            result.Installed.Should().Contain("pdf");
            result.Installed.Should().Contain("review");
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
            $"version = 1\n\n[[skills]]\nname = \"*\"\nsource = \"{repoDir}\"\nexclude = [\"review\"]\n");
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);
            var result = await InstallCommand.RunInstallAsync(new InstallOptions(scope), CT);

            result.Installed.Should().Contain("pdf");
            result.Installed.Should().NotContain("review");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }
}
