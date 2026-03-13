namespace NetAgents.Tests.Cli;

using NetAgents.Tests;
using NetAgents.Cli.Commands;
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
public sealed class AddCommandTests
{
    private static CancellationToken CT => TestContext.Current.CancellationToken;

    private static string SetupProject(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, ".agents", "skills"));
        File.WriteAllText(Path.Combine(root, "agents.toml"), "version = 1\n");
        return root;
    }

    private static async Task<string> CreateRepo(string parentDir, CancellationToken ct, params string[] skillPaths)
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
            File.WriteAllText(Path.Combine(dir, "SKILL.md"), $"---\nname: {name}\ndescription: Test\n---\n");
        }

        await ProcessRunner.RunAsync("git", ["add", "."], repoDir, ct: ct);
        await ProcessRunner.RunAsync("git", ["commit", "-m", "initial"], repoDir, ct: ct);
        return TestWorkspace.ToGitSource(repoDir);
    }

    [Fact]
    public async Task AddsSingleSkill_ViaNames()
    {
        using var tmp = new TempDir();
        var project = SetupProject(Path.Combine(tmp.Path, "project"));
        var repoDir = await CreateRepo(tmp.Path, CT, "pdf", "skills/review");
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);
            var result = await AddCommand.RunAddAsync(new AddOptions(scope, repoDir, Names: ["pdf"]), CT);

            Assert.Equal("pdf", result.SingleName);
            var toml = await File.ReadAllTextAsync(Path.Combine(project, "agents.toml"), CT);
            Assert.Contains("name = \"pdf\"", toml);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }

    [Fact]
    public async Task AddsMultipleSkills_ViaNames()
    {
        using var tmp = new TempDir();
        var project = SetupProject(Path.Combine(tmp.Path, "project"));
        var repoDir = await CreateRepo(tmp.Path, CT, "pdf", "skills/review");
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);
            var result = await AddCommand.RunAddAsync(
                new AddOptions(scope, repoDir, Names: ["pdf", "review"]), CT);

            Assert.NotNull(result.MultipleNames);
            Assert.Contains("pdf", result.MultipleNames);
            Assert.Contains("review", result.MultipleNames);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }

    [Fact]
    public async Task ThrowsWhenSkillNotFound()
    {
        using var tmp = new TempDir();
        var project = SetupProject(Path.Combine(tmp.Path, "project"));
        var repoDir = await CreateRepo(tmp.Path, CT, "pdf");
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);
            await Assert.ThrowsAsync<AddException>(() =>
                AddCommand.RunAddAsync(new AddOptions(scope, repoDir, Names: ["nonexistent"]), CT));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }

    [Fact]
    public async Task ThrowsWhenSkillAlreadyExists()
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
            var ex = await Assert.ThrowsAsync<AddException>(() =>
                AddCommand.RunAddAsync(new AddOptions(scope, repoDir, Names: ["pdf"]), CT));
            Assert.Contains("already exists", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }

    [Fact]
    public async Task ThrowsWhenAllUsedWithNames()
    {
        using var tmp = new TempDir();
        var project = SetupProject(Path.Combine(tmp.Path, "project"));
        var repoDir = await CreateRepo(tmp.Path, CT, "pdf");
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);
            await Assert.ThrowsAsync<AddException>(() =>
                AddCommand.RunAddAsync(new AddOptions(scope, repoDir, Names: ["pdf"], All: true), CT));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }

    [Fact]
    public async Task AutoSelectsSingleSkillRepo()
    {
        using var tmp = new TempDir();
        var project = SetupProject(Path.Combine(tmp.Path, "project"));
        var singleRepo = Path.Combine(tmp.Path, "single-repo");
        Directory.CreateDirectory(singleRepo);
        await ProcessRunner.RunAsync("git", ["init"], singleRepo, ct: CT);
        await ProcessRunner.RunAsync("git", ["config", "user.email", "t@t.com"], singleRepo, ct: CT);
        await ProcessRunner.RunAsync("git", ["config", "user.name", "T"], singleRepo, ct: CT);
        Directory.CreateDirectory(Path.Combine(singleRepo, "only-skill"));
        File.WriteAllText(Path.Combine(singleRepo, "only-skill", "SKILL.md"),
            "---\nname: only-skill\ndescription: T\n---\n");
        await ProcessRunner.RunAsync("git", ["add", "."], singleRepo, ct: CT);
        await ProcessRunner.RunAsync("git", ["commit", "-m", "init"], singleRepo, ct: CT);
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);
            var result = await AddCommand.RunAddAsync(new AddOptions(scope, TestWorkspace.ToGitSource(singleRepo)), CT);

            Assert.Equal("only-skill", result.SingleName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }

    [Fact]
    public async Task ThrowsInNonInteractiveMode_WithMultipleSkills()
    {
        using var tmp = new TempDir();
        var project = SetupProject(Path.Combine(tmp.Path, "project"));
        var repoDir = await CreateRepo(tmp.Path, CT, "pdf", "skills/review");
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);
            var ex = await Assert.ThrowsAsync<AddException>(() =>
                AddCommand.RunAddAsync(new AddOptions(scope, repoDir), CT));
            Assert.Contains("Multiple skills found", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }

    [Fact]
    public async Task AddsSingleLocalSkill_WithoutNames()
    {
        using var tmp = new TempDir();
        var project = SetupProject(Path.Combine(tmp.Path, "project"));
        var skillDir = Path.Combine(project, "my-skill");
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "---\nname: my-skill\ndescription: T\n---\n");

        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);
        var result = await AddCommand.RunAddAsync(new AddOptions(scope, "path:my-skill"), CT);

        Assert.Equal("my-skill", result.SingleName);
    }
}
