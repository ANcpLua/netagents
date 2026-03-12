using System.Text.Json;
using NetAgents.Cli.Commands;
using NetAgents.Config;
using NetAgents.Mcp;
using NetAgents.Utils;
using Xunit;

namespace NetAgents.Tests.Mcp;

file sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
    public TempDir() => Directory.CreateDirectory(Path);
    public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); }
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

        Assert.Contains("[]", result);
    }

    [Fact]
    public async Task ListAsync_WithInstalledSkill_ReturnsSkillInfo()
    {
        using var tmp = new TempDir();
        var project = Path.Combine(tmp.Path, "project");
        Directory.CreateDirectory(Path.Combine(project, ".agents", "skills"));
        var repoDir = await CreateRepo(tmp.Path, CT, "pdf");
        File.WriteAllText(Path.Combine(project, "agents.toml"),
            $"version = 1\n\n[[skills]]\nname = \"pdf\"\nsource = \"git:{repoDir}\"\n");
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var scope = ScopeResolver.ResolveScope(ScopeKind.Project, project);
            await InstallCommand.RunInstallAsync(new InstallOptions(scope), CT);

            var server = new NetAgentsMcpServer();
            var result = await server.ListAsync(project, CT);

            Assert.Contains("pdf", result);
            Assert.Contains("ok", result);
        }
        finally { Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null); }
    }

    [Fact]
    public async Task ListAsync_MissingConfigThrows()
    {
        var server = new NetAgentsMcpServer();
        await Assert.ThrowsAsync<ConfigException>(
            () => server.ListAsync("/nonexistent-path-that-does-not-exist", CT));
    }

    [Fact]
    public async Task InstallAsync_InstallsDeclaredSkills()
    {
        using var tmp = new TempDir();
        var project = Path.Combine(tmp.Path, "project");
        Directory.CreateDirectory(Path.Combine(project, ".agents", "skills"));
        var repoDir = await CreateRepo(tmp.Path, CT, "pdf");
        File.WriteAllText(Path.Combine(project, "agents.toml"),
            $"version = 1\n\n[[skills]]\nname = \"pdf\"\nsource = \"git:{repoDir}\"\n");
        Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", Path.Combine(tmp.Path, "state"));
        try
        {
            var server = new NetAgentsMcpServer();
            var result = await server.InstallAsync(project, CT);

            Assert.Contains("Installed 1 skill(s)", result);
            Assert.Contains("pdf", result);
            Assert.True(Directory.Exists(Path.Combine(project, ".agents", "skills", "pdf")));
        }
        finally { Environment.SetEnvironmentVariable("NETAGENTS_STATE_DIR", null); }
    }

    [Fact]
    public async Task InstallAsync_MissingConfigThrows()
    {
        var server = new NetAgentsMcpServer();
        await Assert.ThrowsAsync<ConfigException>(
            () => server.InstallAsync("/nonexistent-path-that-does-not-exist", CT));
    }

    private static async Task<string> CreateRepo(string parentDir, CancellationToken ct, params string[] skillPaths)
    {
        var repoDir = Path.Combine(parentDir, "repo");
        Directory.CreateDirectory(repoDir);
        await ProcessRunner.RunAsync("git", ["init"], cwd: repoDir, ct: ct);
        await ProcessRunner.RunAsync("git", ["config", "user.email", "t@t.com"], cwd: repoDir, ct: ct);
        await ProcessRunner.RunAsync("git", ["config", "user.name", "T"], cwd: repoDir, ct: ct);
        foreach (var sp in skillPaths)
        {
            var dir = Path.Combine(repoDir, sp);
            Directory.CreateDirectory(dir);
            var name = Path.GetFileName(sp);
            File.WriteAllText(Path.Combine(dir, "SKILL.md"), $"---\nname: {name}\ndescription: T\n---\n");
        }
        await ProcessRunner.RunAsync("git", ["add", "."], cwd: repoDir, ct: ct);
        await ProcessRunner.RunAsync("git", ["commit", "-m", "initial"], cwd: repoDir, ct: ct);
        return repoDir;
    }
}
