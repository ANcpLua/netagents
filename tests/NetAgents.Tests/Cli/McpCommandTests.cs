namespace NetAgents.Tests.Cli;

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
        Assert.Equal("Authorization", key);
        Assert.Equal("Bearer tok", value);
    }

    [Fact]
    public void HandlesColonsInValue()
    {
        var (key, value) = McpCommand.ParseHeader("X-Key:val:ue");
        Assert.Equal("X-Key", key);
        Assert.Equal("val:ue", value);
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
        Assert.Single(config.Mcp);
        Assert.Equal("github", config.Mcp[0].Name);
        Assert.Equal("npx", config.Mcp[0].Command);
        Assert.Equal(["-y", "@modelcontextprotocol/server-github"], config.Mcp[0].Args);
        Assert.Equal(["GITHUB_TOKEN"], config.Mcp[0].Env);
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
        Assert.Single(config.Mcp);
        Assert.Equal("https://mcp.example.com/sse", config.Mcp[0].Url);
    }

    [Fact]
    public async Task RejectsDuplicateName()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);

        await McpCommand.RunMcpAddAsync(new McpCommand.McpAddOptions(scope, "github", "npx"), CT);
        var ex = await Assert.ThrowsAsync<McpException>(() =>
            McpCommand.RunMcpAddAsync(new McpCommand.McpAddOptions(scope, "github", "other"), CT));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task RejectsBothCommandAndUrl()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);

        var ex = await Assert.ThrowsAsync<McpException>(() => McpCommand.RunMcpAddAsync(new McpCommand.McpAddOptions(
            scope, "bad", "npx", Url: "https://example.com"), CT));
        Assert.Contains("Cannot specify both", ex.Message);
    }

    [Fact]
    public async Task RejectsNeitherCommandNorUrl()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            McpCommand.RunMcpAddAsync(new McpCommand.McpAddOptions(scope, "bad"), CT));
        Assert.Contains("Must specify either", ex.Message);
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
        Assert.Empty(config.Mcp);
    }

    [Fact]
    public async Task ThrowsForNonExistentServer()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            McpCommand.RunMcpRemoveAsync(new McpCommand.McpRemoveOptions(scope, "nope"), CT));
        Assert.Contains("not found", ex.Message);
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
        Assert.Single(config.Mcp);
        Assert.Equal("b", config.Mcp[0].Name);
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
        Assert.Empty(McpCommand.GetMcpList(config));
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
        Assert.Single(list);
        Assert.Equal("github", list[0].Name);
        Assert.Equal("stdio", list[0].Transport);
        Assert.Equal("npx", list[0].Target);
        Assert.Equal(["TOKEN"], list[0].Env);
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
        Assert.Single(list);
        Assert.Equal("remote", list[0].Name);
        Assert.Equal("http", list[0].Transport);
        Assert.Equal("https://example.com/mcp", list[0].Target);
    }
}
