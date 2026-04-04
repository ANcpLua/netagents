namespace NetAgents.Tests.Mcp;

using AwesomeAssertions;
using NetAgents.Tests;
using NetAgents.Cli.Commands;
using NetAgents.Config;
using NetAgents.Mcp;
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
public sealed class ServerTests
{
    private static CancellationToken CT => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListAsync_EmptyProject_ReturnsEmptyArray()
    {
        using var tmp = new TempDir();
        var project = Path.Combine(tmp.Path, "project");
        Directory.CreateDirectory(Path.Combine(project, ".agents", "skills"));
        File.WriteAllText(Path.Combine(project, "agents.toml"), "version = 1\n");

        var server = new NetAgentsMcpServer();
        var result = await server.ListAsync(project, CT);

        result.Should().Contain("[]");
    }

    [Fact]
    public async Task ListAsync_WithInstalledSkill_ReturnsSkillInfo()
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

            var server = new NetAgentsMcpServer();
            var result = await server.ListAsync(project, CT);

            result.Should().Contain("pdf");
            result.Should().Contain("ok");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }

    [Fact]
    public async Task ListAsync_MissingConfigThrows()
    {
        var server = new NetAgentsMcpServer();
        await Assert.ThrowsAsync<ConfigException>(() => server.ListAsync("/nonexistent-path-that-does-not-exist", CT));
    }

    [Fact]
    public async Task InstallAsync_InstallsDeclaredSkills()
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
            var server = new NetAgentsMcpServer();
            var result = await server.InstallAsync(project, CT);

            result.Should().Contain("Installed 1 skill(s)");
            result.Should().Contain("pdf");
            Directory.Exists(Path.Combine(project, ".agents", "skills", "pdf")).Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }

    [Fact]
    public async Task InstallAsync_MissingConfigThrows()
    {
        var server = new NetAgentsMcpServer();
        await Assert.ThrowsAsync<ConfigException>(() =>
            server.InstallAsync("/nonexistent-path-that-does-not-exist", CT));
    }

    [Fact]
    public async Task AddAsync_AddsSkillToConfig()
    {
        using var tmp = new TempDir();
        var project = Path.Combine(tmp.Path, "project");
        Directory.CreateDirectory(Path.Combine(project, ".agents", "skills"));
        var repoDir = await CreateRepo(tmp.Path, CT, "pdf");
        File.WriteAllText(Path.Combine(project, "agents.toml"), "version = 1\n");
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var server = new NetAgentsMcpServer();
            var result = await server.AddAsync(project, repoDir, CT);

            result.Should().Contain("Added skill: pdf");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }

    [Fact]
    public async Task AddAsync_InvalidSourceThrows()
    {
        using var tmp = new TempDir();
        var project = Path.Combine(tmp.Path, "project");
        Directory.CreateDirectory(Path.Combine(project, ".agents", "skills"));
        File.WriteAllText(Path.Combine(project, "agents.toml"), "version = 1\n");

        var server = new NetAgentsMcpServer();
        await Assert.ThrowsAsync<AddException>(() => server.AddAsync(project, "not-a-valid-source", CT));
    }

    [Fact]
    public async Task RemoveAsync_RemovesExplicitSkill()
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

            var server = new NetAgentsMcpServer();
            var result = await server.RemoveAsync(project, "pdf", CT);

            result.Should().Contain("Removed skill: pdf");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }

    [Fact]
    public async Task RemoveAsync_NotFoundThrows()
    {
        using var tmp = new TempDir();
        var project = Path.Combine(tmp.Path, "project");
        Directory.CreateDirectory(Path.Combine(project, ".agents", "skills"));
        File.WriteAllText(Path.Combine(project, "agents.toml"), "version = 1\n");

        var server = new NetAgentsMcpServer();
        await Assert.ThrowsAsync<RemoveException>(() => server.RemoveAsync(project, "nonexistent", CT));
    }

    [Fact]
    public async Task RemoveAsync_WildcardReturnsHint()
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

            var server = new NetAgentsMcpServer();
            var result = await server.RemoveAsync(project, "pdf", CT);

            result.Should().Contain("wildcard");
            result.Should().Contain("exclude");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null);
        }
    }

    [Fact]
    public async Task SyncAsync_ReportsIssuesOnDesynchronizedState()
    {
        using var tmp = new TempDir();
        var project = Path.Combine(tmp.Path, "project");
        Directory.CreateDirectory(Path.Combine(project, ".agents", "skills"));
        var repoDir = await CreateRepo(tmp.Path, CT, "pdf");
        File.WriteAllText(Path.Combine(project, "agents.toml"),
            $"version = 1\n\n[[skills]]\nname = \"pdf\"\nsource = \"{repoDir}\"\n");

        var server = new NetAgentsMcpServer();
        var result = await server.SyncAsync(project, CT);

        result.Should().Contain("missing");
    }

    [Fact]
    public async Task DoctorAsync_ReportsChecks()
    {
        using var tmp = new TempDir();
        var project = Path.Combine(tmp.Path, "project");
        Directory.CreateDirectory(Path.Combine(project, ".agents", "skills"));
        File.WriteAllText(Path.Combine(project, "agents.toml"), "version = 1\n");

        var server = new NetAgentsMcpServer();
        var result = await server.DoctorAsync(project, false, CT);

        result.Should().Contain("[");
    }

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
}
