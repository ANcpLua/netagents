namespace NetAgents.Tests.Cli;

using AwesomeAssertions;
using NetAgents.Cli.Commands;
using NetAgents.Config;
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
        if (Directory.Exists(Path)) Directory.Delete(Path, true);
    }
}

public sealed class ValidateMcpNameTests
{
    [Theory]
    [InlineData("github")]
    [InlineData("my-server")]
    [InlineData("server.v2")]
    [InlineData("MCP_Server")]
    public void AcceptsValidNames(string name)
    {
        McpCommand.ValidateMcpName(name);
        // should not throw
    }

    [Theory]
    [InlineData("")]
    [InlineData("-bad")]
    [InlineData(".bad")]
    [InlineData("has space")]
    public void RejectsInvalidNames(string name)
    {
        Assert.Throws<McpException>(() => McpCommand.ValidateMcpName(name));
    }
}

public sealed class ParseHeaderTests
{
    [Fact]
    public void SplitsOnFirstColon()
    {
        var (key, value) = McpCommand.ParseHeader("Authorization:Bearer tok");
        key.Should().Be("Authorization");
        value.Should().Be("Bearer tok");
    }

    [Fact]
    public void HandlesColonsInValue()
    {
        var (key, value) = McpCommand.ParseHeader("X-Key:val:ue");
        key.Should().Be("X-Key");
        value.Should().Be("val:ue");
    }

    [Theory]
    [InlineData("no-colon")]
    [InlineData(":no-key")]
    public void ThrowsOnMalformedHeader(string raw)
    {
        Assert.Throws<McpException>(() => McpCommand.ParseHeader(raw));
    }
}

public sealed class McpAddTests
{
    private static CancellationToken CT => TestContext.Current.CancellationToken;

    private static ScopeRoot SetupScope(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, ".agents", "skills"));
        File.WriteAllText(Path.Combine(root, "agents.toml"), "version = 1\n");
        return new ScopeRoot(ScopeKind.Project, root,
            Path.Combine(root, ".agents"),
            Path.Combine(root, "agents.toml"),
            Path.Combine(root, "agents.lock"),
            Path.Combine(root, ".agents", "skills"));
    }

    [Fact]
    public async Task AddsStdioServer()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);

        await McpCommand.RunMcpAddAsync(new McpCommand.McpAddOptions(
            scope, "github", "npx",
            ["-y", "@modelcontextprotocol/server-github"],
            Env: ["GITHUB_TOKEN"]), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        config.Mcp.Should().ContainSingle();
        config.Mcp[0].Name.Should().Be("github");
        config.Mcp[0].Command.Should().Be("npx");
        config.Mcp[0].Args.Should().BeEquivalentTo(["-y", "@modelcontextprotocol/server-github"]);
        config.Mcp[0].Env.Should().BeEquivalentTo(["GITHUB_TOKEN"]);
    }

    [Fact]
    public async Task AddsHttpServer()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);

        await McpCommand.RunMcpAddAsync(new McpCommand.McpAddOptions(
            scope, "remote", Url: "https://mcp.example.com/sse",
            Headers: ["Authorization:Bearer tok"], Env: ["API_KEY"]), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        config.Mcp.Should().ContainSingle();
        config.Mcp[0].Url.Should().Be("https://mcp.example.com/sse");
    }

    [Fact]
    public async Task RejectsDuplicateName()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);

        await McpCommand.RunMcpAddAsync(new McpCommand.McpAddOptions(scope, "github", "npx"), CT);
        var ex = await Assert.ThrowsAsync<McpException>(() =>
            McpCommand.RunMcpAddAsync(new McpCommand.McpAddOptions(scope, "github", "other"), CT));
        ex.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task RejectsBothCommandAndUrl()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);

        var ex = await Assert.ThrowsAsync<McpException>(() => McpCommand.RunMcpAddAsync(new McpCommand.McpAddOptions(
            scope, "bad", "npx", Url: "https://example.com"), CT));
        ex.Message.Should().Contain("Cannot specify both");
    }

    [Fact]
    public async Task RejectsNeitherCommandNorUrl()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            McpCommand.RunMcpAddAsync(new McpCommand.McpAddOptions(scope, "bad"), CT));
        ex.Message.Should().Contain("Must specify either");
    }

    [Fact]
    public async Task RejectsInvalidName()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);

        await Assert.ThrowsAsync<McpException>(() =>
            McpCommand.RunMcpAddAsync(new McpCommand.McpAddOptions(scope, "-bad", "npx"), CT));
    }
}

public sealed class McpRemoveTests
{
    private static CancellationToken CT => TestContext.Current.CancellationToken;

    private static ScopeRoot SetupScope(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, ".agents", "skills"));
        File.WriteAllText(Path.Combine(root, "agents.toml"), "version = 1\n");
        return new ScopeRoot(ScopeKind.Project, root,
            Path.Combine(root, ".agents"),
            Path.Combine(root, "agents.toml"),
            Path.Combine(root, "agents.lock"),
            Path.Combine(root, ".agents", "skills"));
    }

    [Fact]
    public async Task RemovesExistingServer()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);

        await McpCommand.RunMcpAddAsync(new McpCommand.McpAddOptions(scope, "github", "npx"), CT);
        await McpCommand.RunMcpRemoveAsync(new McpCommand.McpRemoveOptions(scope, "github"), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        config.Mcp.Should().BeEmpty();
    }

    [Fact]
    public async Task ThrowsForNonExistentServer()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            McpCommand.RunMcpRemoveAsync(new McpCommand.McpRemoveOptions(scope, "nope"), CT));
        ex.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task PreservesOtherServers()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);

        await McpCommand.RunMcpAddAsync(new McpCommand.McpAddOptions(scope, "a", "cmd-a"), CT);
        await McpCommand.RunMcpAddAsync(new McpCommand.McpAddOptions(scope, "b", "cmd-b"), CT);
        await McpCommand.RunMcpRemoveAsync(new McpCommand.McpRemoveOptions(scope, "a"), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        config.Mcp.Should().ContainSingle();
        config.Mcp[0].Name.Should().Be("b");
    }
}

public sealed class GetMcpListTests
{
    private static CancellationToken CT => TestContext.Current.CancellationToken;

    private static ScopeRoot SetupScope(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, ".agents", "skills"));
        File.WriteAllText(Path.Combine(root, "agents.toml"), "version = 1\n");
        return new ScopeRoot(ScopeKind.Project, root,
            Path.Combine(root, ".agents"),
            Path.Combine(root, "agents.toml"),
            Path.Combine(root, "agents.lock"),
            Path.Combine(root, ".agents", "skills"));
    }

    [Fact]
    public async Task ReturnsEmpty_ForNoServers()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);
        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        McpCommand.GetMcpList(config).Should().BeEmpty();
    }

    [Fact]
    public async Task ReturnsStdioEntries()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);
        await McpCommand.RunMcpAddAsync(new McpCommand.McpAddOptions(
            scope, "github", "npx", Env: ["TOKEN"]), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        var list = McpCommand.GetMcpList(config);
        list.Should().ContainSingle();
        list[0].Name.Should().Be("github");
        list[0].Transport.Should().Be("stdio");
        list[0].Target.Should().Be("npx");
        list[0].Env.Should().BeEquivalentTo(["TOKEN"]);
    }

    [Fact]
    public async Task ReturnsHttpEntries()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);
        await McpCommand.RunMcpAddAsync(new McpCommand.McpAddOptions(
            scope, "remote", Url: "https://example.com/mcp"), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        var list = McpCommand.GetMcpList(config);
        list.Should().ContainSingle();
        list[0].Name.Should().Be("remote");
        list[0].Transport.Should().Be("http");
        list[0].Target.Should().Be("https://example.com/mcp");
    }
}
