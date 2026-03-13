namespace NetAgents.Tests.Agents;

using System.Text.Json.Nodes;
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
        Assert.Equal(5, ids.Count);
        Assert.Contains("claude", ids);
        Assert.Contains("cursor", ids);
        Assert.Contains("codex", ids);
        Assert.Contains("vscode", ids);
        Assert.Contains("opencode", ids);
    }

    // ── getAgent ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetAgent_ReturnsNullForUnknown()
    {
        Assert.Null(AgentRegistry.GetAgent("unknown"));
    }

    // ── claude serializer ────────────────────────────────────────────────────

    [Fact]
    public void Claude_SerializesStdioServer()
    {
        var agent = AgentRegistry.GetAgent("claude")!;
        var (name, config) = agent.SerializeServer(StdioServer);
        Assert.Equal("github", name);

        var obj = (JsonObject)config;
        Assert.Equal("npx", obj["command"]!.GetValue<string>());
        Assert.Equal("${GITHUB_TOKEN}", obj["env"]!["GITHUB_TOKEN"]!.GetValue<string>());
    }

    [Fact]
    public void Claude_SerializesHttpServer()
    {
        var agent = AgentRegistry.GetAgent("claude")!;
        var (name, config) = agent.SerializeServer(HttpServer);
        Assert.Equal("remote-api", name);

        var obj = (JsonObject)config;
        Assert.Equal("http", obj["type"]!.GetValue<string>());
        Assert.Equal("https://mcp.example.com/sse", obj["url"]!.GetValue<string>());
        Assert.Equal("Bearer tok", obj["headers"]!["Authorization"]!.GetValue<string>());
    }

    [Fact]
    public void Claude_OmitsEnvWhenEmpty()
    {
        var agent = AgentRegistry.GetAgent("claude")!;
        var (_, config) = agent.SerializeServer(StdioNoEnv);

        var obj = (JsonObject)config;
        Assert.Equal("mcp-server", obj["command"]!.GetValue<string>());
        Assert.Null(obj["env"]);
    }

    // ── cursor serializer ────────────────────────────────────────────────────

    [Fact]
    public void Cursor_SerializesStdioSameAsClaude()
    {
        var cursor = AgentRegistry.GetAgent("cursor")!;
        var claude = AgentRegistry.GetAgent("claude")!;

        var (cn, cc) = cursor.SerializeServer(StdioServer);
        var (an, ac) = claude.SerializeServer(StdioServer);

        Assert.Equal(an, cn);
        Assert.Equal(ac.ToString(), cc.ToString());
    }

    [Fact]
    public void Cursor_SerializesHttpWithoutTypeField()
    {
        var agent = AgentRegistry.GetAgent("cursor")!;
        var (name, config) = agent.SerializeServer(HttpServer);
        Assert.Equal("remote-api", name);

        var obj = (JsonObject)config;
        Assert.Null(obj["type"]);
        Assert.Equal("https://mcp.example.com/sse", obj["url"]!.GetValue<string>());
    }

    // ── codex serializer ─────────────────────────────────────────────────────

    [Fact]
    public void Codex_SerializesStdioServer()
    {
        var agent = AgentRegistry.GetAgent("codex")!;
        var (name, config) = agent.SerializeServer(StdioServer);
        Assert.Equal("github", name);

        var obj = (JsonObject)config;
        Assert.Equal("npx", obj["command"]!.GetValue<string>());
        Assert.Equal("${GITHUB_TOKEN}", obj["env"]!["GITHUB_TOKEN"]!.GetValue<string>());
    }

    [Fact]
    public void Codex_SerializesHttpWithHttpHeaders()
    {
        var agent = AgentRegistry.GetAgent("codex")!;
        var (name, config) = agent.SerializeServer(HttpServer);
        Assert.Equal("remote-api", name);

        var obj = (JsonObject)config;
        Assert.Null(obj["type"]);
        Assert.Equal("https://mcp.example.com/sse", obj["url"]!.GetValue<string>());
        Assert.Equal("Bearer tok", obj["http_headers"]!["Authorization"]!.GetValue<string>());
    }

    [Fact]
    public void Codex_HasTomlFormatAndShared()
    {
        var agent = AgentRegistry.GetAgent("codex")!;
        Assert.Equal(ConfigFormat.Toml, agent.Mcp.Format);
        Assert.True(agent.Mcp.Shared);
    }

    // ── vscode serializer ────────────────────────────────────────────────────

    [Fact]
    public void VsCode_SerializesStdioWithInputRefs()
    {
        var agent = AgentRegistry.GetAgent("vscode")!;
        var (name, config) = agent.SerializeServer(StdioServer);
        Assert.Equal("github", name);

        var obj = (JsonObject)config;
        Assert.Equal("stdio", obj["type"]!.GetValue<string>());
        Assert.Equal("npx", obj["command"]!.GetValue<string>());
        Assert.Equal("${input:GITHUB_TOKEN}", obj["env"]!["GITHUB_TOKEN"]!.GetValue<string>());
    }

    [Fact]
    public void VsCode_SerializesHttpWithHttpType()
    {
        var agent = AgentRegistry.GetAgent("vscode")!;
        var (_, config) = agent.SerializeServer(HttpServer);

        var obj = (JsonObject)config;
        Assert.Equal("http", obj["type"]!.GetValue<string>());
    }

    [Fact]
    public void VsCode_OmitsEnvWhenEmpty()
    {
        var agent = AgentRegistry.GetAgent("vscode")!;
        var (_, config) = agent.SerializeServer(StdioNoEnv);

        var obj = (JsonObject)config;
        Assert.Equal("stdio", obj["type"]!.GetValue<string>());
        Assert.Null(obj["env"]);
    }

    // ── opencode serializer ──────────────────────────────────────────────────

    [Fact]
    public void OpenCode_SerializesStdioAsLocalWithMergedCommand()
    {
        var agent = AgentRegistry.GetAgent("opencode")!;
        var (name, config) = agent.SerializeServer(StdioServer);
        Assert.Equal("github", name);

        var obj = (JsonObject)config;
        Assert.Equal("local", obj["type"]!.GetValue<string>());
        var cmd = obj["command"]!.AsArray();
        Assert.Equal("npx", cmd[0]!.GetValue<string>());
        Assert.Equal("-y", cmd[1]!.GetValue<string>());
        Assert.Equal("${GITHUB_TOKEN}", obj["environment"]!["GITHUB_TOKEN"]!.GetValue<string>());
    }

    [Fact]
    public void OpenCode_SerializesHttpAsRemoteType()
    {
        var agent = AgentRegistry.GetAgent("opencode")!;
        var (_, config) = agent.SerializeServer(HttpServer);

        var obj = (JsonObject)config;
        Assert.Equal("remote", obj["type"]!.GetValue<string>());
    }

    [Fact]
    public void OpenCode_OmitsEnvironmentWhenNoEnv()
    {
        var agent = AgentRegistry.GetAgent("opencode")!;
        var (_, config) = agent.SerializeServer(StdioNoEnv);

        var obj = (JsonObject)config;
        Assert.Equal("local", obj["type"]!.GetValue<string>());
        Assert.Null(obj["environment"]);
    }

    [Fact]
    public void OpenCode_SharedConfigReadsAgentsNatively()
    {
        var agent = AgentRegistry.GetAgent("opencode")!;
        Assert.True(agent.Mcp.Shared);
        Assert.Null(agent.SkillsParentDir);
    }
}
