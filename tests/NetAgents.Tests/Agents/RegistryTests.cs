namespace NetAgents.Tests.Agents;

using System.Text.Json.Nodes;
using AwesomeAssertions;
using NetAgents.Agents;
using Xunit;

public class RegistryTests
{
    private static readonly McpDeclaration StdioServer = new(
        "github", "npx", ["-y", "@modelcontextprotocol/server-github"], Env: ["GITHUB_TOKEN"]);

    private static readonly McpDeclaration HttpServer = new(
        "remote-api", Url: "https://mcp.example.com/sse",
        Headers: new Dictionary<string, string> { ["Authorization"] = "Bearer tok" });

    private static readonly McpDeclaration StdioNoEnv = new(
        "simple", "mcp-server", []);

    // ── allAgentIds ──────────────────────────────────────────────────────────

    [Fact]
    public void AllAgentIds_ReturnsFiveAgents()
    {
        var ids = AgentRegistry.AllAgentIds();
        ids.Count.Should().Be(5);
        ids.Should().Contain("claude");
        ids.Should().Contain("cursor");
        ids.Should().Contain("codex");
        ids.Should().Contain("vscode");
        ids.Should().Contain("opencode");
    }

    // ── getAgent ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetAgent_ReturnsNullForUnknown()
    {
        AgentRegistry.GetAgent("unknown").Should().BeNull();
    }

    // ── claude serializer ────────────────────────────────────────────────────

    [Fact]
    public void Claude_SerializesStdioServer()
    {
        var agent = AgentRegistry.GetAgent("claude")!;
        var (name, config) = agent.SerializeServer(StdioServer);
        name.Should().Be("github");

        var obj = (JsonObject)config;
        obj["command"]!.GetValue<string>().Should().Be("npx");
        obj["env"]!["GITHUB_TOKEN"]!.GetValue<string>().Should().Be("${GITHUB_TOKEN}");
    }

    [Fact]
    public void Claude_SerializesHttpServer()
    {
        var agent = AgentRegistry.GetAgent("claude")!;
        var (name, config) = agent.SerializeServer(HttpServer);
        name.Should().Be("remote-api");

        var obj = (JsonObject)config;
        obj["type"]!.GetValue<string>().Should().Be("http");
        obj["url"]!.GetValue<string>().Should().Be("https://mcp.example.com/sse");
        obj["headers"]!["Authorization"]!.GetValue<string>().Should().Be("Bearer tok");
    }

    [Fact]
    public void Claude_OmitsEnvWhenEmpty()
    {
        var agent = AgentRegistry.GetAgent("claude")!;
        var (_, config) = agent.SerializeServer(StdioNoEnv);

        var obj = (JsonObject)config;
        obj["command"]!.GetValue<string>().Should().Be("mcp-server");
        obj["env"].Should().BeNull();
    }

    // ── cursor serializer ────────────────────────────────────────────────────

    [Fact]
    public void Cursor_SerializesStdioSameAsClaude()
    {
        var cursor = AgentRegistry.GetAgent("cursor")!;
        var claude = AgentRegistry.GetAgent("claude")!;

        var (cn, cc) = cursor.SerializeServer(StdioServer);
        var (an, ac) = claude.SerializeServer(StdioServer);

        cn.Should().Be(an);
        cc.ToString().Should().Be(ac.ToString());
    }

    [Fact]
    public void Cursor_SerializesHttpWithoutTypeField()
    {
        var agent = AgentRegistry.GetAgent("cursor")!;
        var (name, config) = agent.SerializeServer(HttpServer);
        name.Should().Be("remote-api");

        var obj = (JsonObject)config;
        obj["type"].Should().BeNull();
        obj["url"]!.GetValue<string>().Should().Be("https://mcp.example.com/sse");
    }

    // ── codex serializer ─────────────────────────────────────────────────────

    [Fact]
    public void Codex_SerializesStdioServer()
    {
        var agent = AgentRegistry.GetAgent("codex")!;
        var (name, config) = agent.SerializeServer(StdioServer);
        name.Should().Be("github");

        var obj = (JsonObject)config;
        obj["command"]!.GetValue<string>().Should().Be("npx");
        obj["env"]!["GITHUB_TOKEN"]!.GetValue<string>().Should().Be("${GITHUB_TOKEN}");
    }

    [Fact]
    public void Codex_SerializesHttpWithHttpHeaders()
    {
        var agent = AgentRegistry.GetAgent("codex")!;
        var (name, config) = agent.SerializeServer(HttpServer);
        name.Should().Be("remote-api");

        var obj = (JsonObject)config;
        obj["type"].Should().BeNull();
        obj["url"]!.GetValue<string>().Should().Be("https://mcp.example.com/sse");
        obj["http_headers"]!["Authorization"]!.GetValue<string>().Should().Be("Bearer tok");
    }

    [Fact]
    public void Codex_HasTomlFormatAndShared()
    {
        var agent = AgentRegistry.GetAgent("codex")!;
        agent.Mcp.Format.Should().Be(ConfigFormat.Toml);
        agent.Mcp.Shared.Should().BeTrue();
    }

    // ── vscode serializer ────────────────────────────────────────────────────

    [Fact]
    public void VsCode_SerializesStdioWithInputRefs()
    {
        var agent = AgentRegistry.GetAgent("vscode")!;
        var (name, config) = agent.SerializeServer(StdioServer);
        name.Should().Be("github");

        var obj = (JsonObject)config;
        obj["type"]!.GetValue<string>().Should().Be("stdio");
        obj["command"]!.GetValue<string>().Should().Be("npx");
        obj["env"]!["GITHUB_TOKEN"]!.GetValue<string>().Should().Be("${input:GITHUB_TOKEN}");
    }

    [Fact]
    public void VsCode_SerializesHttpWithHttpType()
    {
        var agent = AgentRegistry.GetAgent("vscode")!;
        var (_, config) = agent.SerializeServer(HttpServer);

        var obj = (JsonObject)config;
        obj["type"]!.GetValue<string>().Should().Be("http");
    }

    [Fact]
    public void VsCode_OmitsEnvWhenEmpty()
    {
        var agent = AgentRegistry.GetAgent("vscode")!;
        var (_, config) = agent.SerializeServer(StdioNoEnv);

        var obj = (JsonObject)config;
        obj["type"]!.GetValue<string>().Should().Be("stdio");
        obj["env"].Should().BeNull();
    }

    // ── opencode serializer ──────────────────────────────────────────────────

    [Fact]
    public void OpenCode_SerializesStdioAsLocalWithMergedCommand()
    {
        var agent = AgentRegistry.GetAgent("opencode")!;
        var (name, config) = agent.SerializeServer(StdioServer);
        name.Should().Be("github");

        var obj = (JsonObject)config;
        obj["type"]!.GetValue<string>().Should().Be("local");
        var cmd = obj["command"]!.AsArray();
        cmd[0]!.GetValue<string>().Should().Be("npx");
        cmd[1]!.GetValue<string>().Should().Be("-y");
        obj["environment"]!["GITHUB_TOKEN"]!.GetValue<string>().Should().Be("${GITHUB_TOKEN}");
    }

    [Fact]
    public void OpenCode_SerializesHttpAsRemoteType()
    {
        var agent = AgentRegistry.GetAgent("opencode")!;
        var (_, config) = agent.SerializeServer(HttpServer);

        var obj = (JsonObject)config;
        obj["type"]!.GetValue<string>().Should().Be("remote");
    }

    [Fact]
    public void OpenCode_OmitsEnvironmentWhenNoEnv()
    {
        var agent = AgentRegistry.GetAgent("opencode")!;
        var (_, config) = agent.SerializeServer(StdioNoEnv);

        var obj = (JsonObject)config;
        obj["type"]!.GetValue<string>().Should().Be("local");
        obj["environment"].Should().BeNull();
    }

    [Fact]
    public void OpenCode_SharedConfigReadsAgentsNatively()
    {
        var agent = AgentRegistry.GetAgent("opencode")!;
        agent.Mcp.Shared.Should().BeTrue();
        agent.SkillsParentDir.Should().BeNull();
    }
}
