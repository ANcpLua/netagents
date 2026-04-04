namespace NetAgents.Tests.Cli;

using AwesomeAssertions;
using NetAgents.Tests;
using NetAgents.Cli.Commands;
using NetAgents.Config;
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

[Collection("SerialGit")]
public sealed class RemoveCommandTests
{
    private static CancellationToken CT => TestContext.Current.CancellationToken;

    private static async Task<string> CreateRepo(string parentDir, CancellationToken ct, params string[] skillPaths)
    {
        var repoDir = Path.Combine(parentDir, "repo");
        Directory.CreateDirectory(repoDir);
        await ProcessRunner.RunAsync("git", ["init"], repoDir, ct: ct);
        await ProcessRunner.RunAsync("git", ["config", "user.email", "t@t.com"], repoDir, ct: ct);
        await ProcessRunner.RunAsync("git", ["config", "user.name", "T"], repoDir, ct: ct);
        foreach (var sp in skillPaths)
        {
            var dir = Path.Combine(repoDir, sp);
            Directory.CreateDirectory(dir);
            var name = Path.GetFileName(sp);
            File.WriteAllText(Path.Combine(dir, "SKILL.md"), $"---\nname: {name}\ndescription: T\n---\n");
        }

        await ProcessRunner.RunAsync("git", ["add", "."], repoDir, ct: ct);
        await ProcessRunner.RunAsync("git", ["commit", "-m", "initial"], repoDir, ct: ct);
        return TestWorkspace.ToGitSource(repoDir);
    }

    [Fact]
    public async Task RemovesExplicitSkillEntry()
    {
        using var tmp = new TempDir();
        var project = Path.Combine(tmp.Path, "project");
        Directory.CreateDirectory(Path.Combine(project, ".agents", "skills"));
        var repoDir = await CreateRepo(tmp.Path, CT, "pdf");
        File.WriteAllText(Path.Combine(project, "agents.toml"),
            $"version = 1\n\n[[skills]]\nname = \"pdf\"\nsource = \"{repoDir}\"\n");
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);
            await InstallCommand.RunInstallAsync(new InstallOptions(scope), CT);
            var result = await RemoveCommand.RunRemoveAsync(new RemoveOptions(scope, "pdf"), CT);
            result.Removed.Should().BeTrue();
            result.IsWildcard.Should().BeFalse();

            var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
            config.Skills.OfType<RegularSkillDependency>().Should().NotContain(s => s.Name == "pdf");
            Directory.Exists(Path.Combine(project, ".agents", "skills", "pdf")).Should().BeFalse();

            var lockfile = await LockfileLoader.LoadAsync(scope.LockPath, CT);
            lockfile.Should().NotBeNull();
            lockfile!.Skills.ContainsKey("pdf").Should().BeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }

    [Fact]
    public async Task ThrowsRemoveException_ForSkillNotInConfig()
    {
        using var tmp = new TempDir();
        var project = Path.Combine(tmp.Path, "project");
        Directory.CreateDirectory(Path.Combine(project, ".agents", "skills"));
        File.WriteAllText(Path.Combine(project, "agents.toml"), "version = 1\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);

        await Assert.ThrowsAsync<RemoveException>(() =>
            RemoveCommand.RunRemoveAsync(new RemoveOptions(scope, "nonexistent"), CT));
    }

    [Fact]
    public async Task ReturnsWildcardResult_ForWildcardSourced()
    {
        using var tmp = new TempDir();
        var project = Path.Combine(tmp.Path, "project");
        Directory.CreateDirectory(Path.Combine(project, ".agents", "skills"));
        var repoDir = await CreateRepo(tmp.Path, CT, "pdf");
        File.WriteAllText(Path.Combine(project, "agents.toml"),
            $"version = 1\n\n[[skills]]\nname = \"*\"\nsource = \"{repoDir}\"\n");
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);
            await InstallCommand.RunInstallAsync(new InstallOptions(scope), CT);

            var result = await RemoveCommand.RunRemoveAsync(new RemoveOptions(scope, "pdf"), CT);
            result.Removed.Should().BeFalse();
            result.IsWildcard.Should().BeTrue();
            result.WildcardSource.Should().NotBeNull();
            result.Hint.Should().NotBeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }

    [Fact]
    public async Task RemovesExplicitEntry_EvenWhenWildcardExists()
    {
        using var tmp = new TempDir();
        var project = Path.Combine(tmp.Path, "project");
        Directory.CreateDirectory(Path.Combine(project, ".agents", "skills"));
        var repoDir = await CreateRepo(tmp.Path, CT, "pdf", "skills/review");
        File.WriteAllText(Path.Combine(project, "agents.toml"),
            $"version = 1\n\n[[skills]]\nname = \"pdf\"\nsource = \"{repoDir}\"\n\n[[skills]]\nname = \"*\"\nsource = \"{repoDir}\"\n");
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);
            await InstallCommand.RunInstallAsync(new InstallOptions(scope), CT);
            var result = await RemoveCommand.RunRemoveAsync(new RemoveOptions(scope, "pdf"), CT);
            result.Removed.Should().BeTrue();

            var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
            config.Skills.OfType<RegularSkillDependency>().Should().NotContain(s => s.Name == "pdf");
            config.Skills.OfType<WildcardSkillDependency>().Any().Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }
}
