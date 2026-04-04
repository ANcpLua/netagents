namespace NetAgents.Tests.Agents;

using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using NetAgents.Agents;
using Xunit;

public class McpWriterTests : IAsyncLifetime
{
    private static readonly McpDeclaration Stdio = new(
        "github", "npx", ["-y", "@mcp/server-github"], Env: ["GITHUB_TOKEN"]);

    private static readonly McpDeclaration Http = new(
        "remote", Url: "https://mcp.example.com/sse",
        Headers: new Dictionary<string, string> { ["Authorization"] = "Bearer tok" });

    private string _dir = null!;
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private McpTargetResolver Resolver => McpWriter.ProjectResolver(_dir);

    public async ValueTask InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_dir);
        await ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        await ValueTask.CompletedTask;
    }

    private async Task<JsonObject> ReadJson(params string[] parts)
    {
        var raw = await File.ReadAllTextAsync(Path.Combine(_dir, Path.Combine(parts)), Ct);
        return JsonNode.Parse(raw)!.AsObject();
    }

    // ── write tests ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SkipsWhenNoServers()
    {
        await McpWriter.WriteMcpConfigsAsync(["claude"], [], Resolver, Ct);
        File.Exists(Path.Combine(_dir, ".mcp.json")).Should().BeFalse();
    }

    [Fact]
    public async Task WritesClaudeMcpJson()
    {
        await McpWriter.WriteMcpConfigsAsync(["claude"], [Stdio], Resolver, Ct);

        var doc = await ReadJson(".mcp.json");
        var server = doc["mcpServers"]!["github"]!.AsObject();
        server["command"]!.GetValue<string>().Should().Be("npx");
        server["env"]!["GITHUB_TOKEN"]!.GetValue<string>().Should().Be("${GITHUB_TOKEN}");
    }

    [Fact]
    public async Task WritesCursorMcpJson()
    {
        await McpWriter.WriteMcpConfigsAsync(["cursor"], [Stdio], Resolver, Ct);
        File.Exists(Path.Combine(_dir, ".cursor", "mcp.json")).Should().BeTrue();
    }

    [Fact]
    public async Task WritesVsCodeWithInputRefs()
    {
        await McpWriter.WriteMcpConfigsAsync(["vscode"], [Stdio], Resolver, Ct);

        var doc = await ReadJson(".vscode", "mcp.json");
        var server = doc["servers"]!["github"]!.AsObject();
        server["type"]!.GetValue<string>().Should().Be("stdio");
        server["env"]!["GITHUB_TOKEN"]!.GetValue<string>().Should().Be("${input:GITHUB_TOKEN}");
    }

    [Fact]
    public async Task WritesCodexToml()
    {
        await McpWriter.WriteMcpConfigsAsync(["codex"], [Stdio], Resolver, Ct);

        var raw = await File.ReadAllTextAsync(Path.Combine(_dir, ".codex", "config.toml"), Ct);
        raw.Should().Contain("mcp_servers");
        raw.Should().Contain("github");
    }

    [Fact]
    public async Task WritesOpenCodeJson()
    {
        await McpWriter.WriteMcpConfigsAsync(["opencode"], [Stdio], Resolver, Ct);

        var doc = await ReadJson("opencode.json");
        var server = doc["mcp"]!["github"]!.AsObject();
        server["type"]!.GetValue<string>().Should().Be("local");
    }

    [Fact]
    public async Task HandlesMultipleAgents()
    {
        await McpWriter.WriteMcpConfigsAsync(["claude", "cursor", "vscode"], [Stdio], Resolver, Ct);

        File.Exists(Path.Combine(_dir, ".mcp.json")).Should().BeTrue();
        File.Exists(Path.Combine(_dir, ".cursor", "mcp.json")).Should().BeTrue();
        File.Exists(Path.Combine(_dir, ".vscode", "mcp.json")).Should().BeTrue();
    }

    [Fact]
    public async Task HandlesMultipleServers()
    {
        await McpWriter.WriteMcpConfigsAsync(["claude"], [Stdio, Http], Resolver, Ct);

        var doc = await ReadJson(".mcp.json");
        var servers = doc["mcpServers"]!.AsObject();
        servers.Count.Should().Be(2);
        servers["github"].Should().NotBeNull();
        servers["remote"].Should().NotBeNull();
    }

    [Fact]
    public async Task WritesClaudeHttpWithTypeHttp()
    {
        await McpWriter.WriteMcpConfigsAsync(["claude"], [Http], Resolver, Ct);

        var doc = await ReadJson(".mcp.json");
        var server = doc["mcpServers"]!["remote"]!.AsObject();
        server["type"]!.GetValue<string>().Should().Be("http");
    }

    [Fact]
    public async Task WritesCursorHttpWithoutType()
    {
        await McpWriter.WriteMcpConfigsAsync(["cursor"], [Http], Resolver, Ct);

        var doc = await ReadJson(".cursor", "mcp.json");
        var server = doc["mcpServers"]!["remote"]!.AsObject();
        server["type"].Should().BeNull();
        server["url"]!.GetValue<string>().Should().Be("https://mcp.example.com/sse");
    }

    [Fact]
    public async Task WritesCodexHttpWithHttpHeaders()
    {
        await McpWriter.WriteMcpConfigsAsync(["codex"], [Http], Resolver, Ct);

        var raw = await File.ReadAllTextAsync(Path.Combine(_dir, ".codex", "config.toml"), Ct);
        raw.Should().Contain("http_headers");
        raw.Should().Contain("Bearer tok");
    }

    [Fact]
    public async Task MergesIntoExistingSharedConfig()
    {
        var codexDir = Path.Combine(_dir, ".codex");
        Directory.CreateDirectory(codexDir);
        await File.WriteAllTextAsync(Path.Combine(codexDir, "config.toml"), "model = \"o3\"\n", Ct);

        await McpWriter.WriteMcpConfigsAsync(["codex"], [Stdio], Resolver, Ct);

        var raw = await File.ReadAllTextAsync(Path.Combine(codexDir, "config.toml"), Ct);
        raw.Should().Contain("model");
        raw.Should().Contain("mcp_servers");
    }

    [Fact]
    public async Task PreservesUserServersInSharedConfig()
    {
        var existing = new JsonObject
        {
            ["mcp"] = new JsonObject
                { ["my-custom-server"] = new JsonObject { ["type"] = "local", ["command"] = new JsonArray("my-tool") } }
        };
        await File.WriteAllTextAsync(Path.Combine(_dir, "opencode.json"),
            JsonSerializer.Serialize(existing, new JsonSerializerOptions { WriteIndented = true }), Ct);

        await McpWriter.WriteMcpConfigsAsync(["opencode"], [Stdio], Resolver, Ct);

        var doc = await ReadJson("opencode.json");
        doc["mcp"]!["github"].Should().NotBeNull();
        doc["mcp"]!["my-custom-server"].Should().NotBeNull();
    }

    [Fact]
    public async Task IsIdempotent()
    {
        await McpWriter.WriteMcpConfigsAsync(["claude"], [Stdio], Resolver, Ct);
        var first = await File.ReadAllTextAsync(Path.Combine(_dir, ".mcp.json"), Ct);

        await McpWriter.WriteMcpConfigsAsync(["claude"], [Stdio], Resolver, Ct);
        var second = await File.ReadAllTextAsync(Path.Combine(_dir, ".mcp.json"), Ct);

        second.Should().Be(first);
    }

    [Fact]
    public async Task CreatesParentDirectories()
    {
        await McpWriter.WriteMcpConfigsAsync(["cursor"], [Stdio], Resolver, Ct);
        File.Exists(Path.Combine(_dir, ".cursor", "mcp.json")).Should().BeTrue();
    }

    // ── verify tests ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Verify_NoIssuesWhenConfigsMatch()
    {
        await McpWriter.WriteMcpConfigsAsync(["claude"], [Stdio], Resolver, Ct);
        var issues = await McpWriter.VerifyMcpConfigsAsync(["claude"], [Stdio], Resolver, Ct);
        issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Verify_ReportsMissingConfigFile()
    {
        var issues = await McpWriter.VerifyMcpConfigsAsync(["claude"], [Stdio], Resolver, Ct);
        issues.Should().ContainSingle();
        issues[0].Issue.Should().Contain("missing");
    }

    [Fact]
    public async Task Verify_ReportsMissingServer()
    {
        await McpWriter.WriteMcpConfigsAsync(["claude"], [Stdio], Resolver, Ct);
        var issues = await McpWriter.VerifyMcpConfigsAsync(["claude"], [Stdio, Http], Resolver, Ct);
        issues.Should().Contain(i => i.Issue.Contains("remote"));
    }

    [Fact]
    public async Task Verify_EmptyWhenNoServers()
    {
        var issues = await McpWriter.VerifyMcpConfigsAsync(["claude"], [], Resolver, Ct);
        issues.Should().BeEmpty();
    }
}
