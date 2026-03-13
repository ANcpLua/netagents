namespace NetAgents.Tests.Cli;

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
            Assert.True(result.Removed);
            Assert.False(result.IsWildcard);

            var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
            Assert.DoesNotContain(config.Skills.OfType<RegularSkillDependency>(), s => s.Name == "pdf");
            Assert.False(Directory.Exists(Path.Combine(project, ".agents", "skills", "pdf")));

            var lockfile = await LockfileLoader.LoadAsync(scope.LockPath, CT);
            Assert.NotNull(lockfile);
            Assert.False(lockfile.Skills.ContainsKey("pdf"));
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
            Assert.False(result.Removed);
            Assert.True(result.IsWildcard);
            Assert.NotNull(result.WildcardSource);
            Assert.NotNull(result.Hint);
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
            Assert.True(result.Removed);

            var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
            Assert.DoesNotContain(config.Skills.OfType<RegularSkillDependency>(), s => s.Name == "pdf");
            Assert.True(config.Skills.OfType<WildcardSkillDependency>().Any());
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }
}
