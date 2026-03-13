namespace Qyl.Agents.Tests;

using System.Diagnostics;
using System.Text.Json;
using ANcpLua.Roslyn.Utilities.Testing.AgentTesting;
using Protocol;
using Xunit;

public sealed class McpProtocolEndToEndTests
{
    private readonly McpProtocolHandler<CalcServer> _handler;
    private readonly CalcServer _server = new();

    public McpProtocolEndToEndTests()
    {
        _handler = new McpProtocolHandler<CalcServer>(_server);
    }

    [Fact]
    public async Task InitializeReturnsServerInfo()
    {
        var request = MakeRequest("initialize");
        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Null(response!.Error);
        Assert.NotNull(response.Result);

        var serverInfo = response.Result.Value.GetProperty("serverInfo");
        Assert.Equal((string?)"calc-server", (string?)serverInfo.GetProperty("name").GetString());
    }

    [Fact]
    public async Task ToolsListReturnsAllTools()
    {
        var request = MakeRequest("tools/list");
        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        var tools = response!.Result!.Value.GetProperty("tools");
        Assert.Equal(3, tools.GetArrayLength());

        var toolNames = tools.EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()!)
            .OrderBy(n => n)
            .ToList();
        Assert.Contains("add", toolNames);
        Assert.Contains("multiply", toolNames);
        Assert.Contains("fail", toolNames);
    }

    [Fact]
    public async Task ToolsCallAddReturnsResult()
    {
        var args = JsonDocument.Parse("""{"a": 3, "b": 4}""").RootElement;
        var request = MakeToolCallRequest("add", args);
        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Null(response!.Error);

        var content = response.Result!.Value.GetProperty("content");
        var text = content[0].GetProperty("text").GetString();
        Assert.Equal((string?)"7", (string?)text);
    }

    [Fact]
    public async Task ToolsCallMultiplyReturnsResult()
    {
        var args = JsonDocument.Parse("""{"a": 5, "b": 6}""").RootElement;
        var request = MakeToolCallRequest("multiply", args);
        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        var content = response!.Result!.Value.GetProperty("content");
        var text = content[0].GetProperty("text").GetString();
        Assert.Equal((string?)"30", (string?)text);
    }

    [Fact]
    public async Task ToolsCallUnknownToolReturnsError()
    {
        var args = JsonDocument.Parse("{}").RootElement;
        var request = MakeToolCallRequest("nonexistent", args);
        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        // Unknown tools throw ArgumentException, which the protocol handler maps to a JSON-RPC error
        Assert.NotNull(response!.Error);
        Assert.Equal(McpErrorCodes.InvalidParams, response.Error!.Code);
    }

    [Fact]
    public async Task ToolCallEmitsOTelSpan()
    {
        using var collector = new ActivityCollector("Qyl.Agents");

        var args = JsonDocument.Parse("""{"a": 1, "b": 2}""").RootElement;
        var request = MakeToolCallRequest("add", args);
        await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        var span = collector.FindSingle("execute_tool add");
        span.AssertTag("gen_ai.operation.name", "execute_tool");
        span.AssertTag("gen_ai.tool.name", "add");
        span.AssertTag("gen_ai.tool.type", "function");
        span.AssertStatus(ActivityStatusCode.Ok);
    }

    [Fact]
    public void SkillMdContainsFrontmatterAndTools()
    {
        var skillMd = CalcServer.SkillMd;

        // YAML frontmatter structure
        Assert.Contains("---", skillMd);
        Assert.Contains("name: calc-server", skillMd);

        // Tool sections
        Assert.Contains("### add", skillMd);
        Assert.Contains("### multiply", skillMd);

        // Parameter docs
        Assert.Contains("`a` (integer, required): First number", skillMd);
        Assert.Contains("`b` (integer, required): Second number", skillMd);
        Assert.Contains("`a` (integer, required): First factor", skillMd);
        Assert.Contains("`b` (integer, required): Second factor", skillMd);
    }

    [Fact]
    public async Task NotificationReturnsNull()
    {
        var request = new JsonRpcRequest { Method = "notifications/initialized" };
        var response = await _handler.HandleAsync(request, CancellationToken.None);
        Assert.Null(response);
    }

    [Fact]
    public async Task RepeatedToolsListReturnsConsistentSchema()
    {
        var request = MakeRequest("tools/list");

        var response1 = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);
        var response2 = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        var json1 = response1!.Result!.Value.GetRawText();
        var json2 = response2!.Result!.Value.GetRawText();
        Assert.Equal(json1, json2);
    }

    [Fact]
    public async Task ToolExceptionReturnsIsErrorContent()
    {
        var args = JsonDocument.Parse("""{"message": "boom"}""").RootElement;
        var request = MakeToolCallRequest("fail", args);
        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Null(response!.Error); // MCP spec: tool errors are content, not JSON-RPC errors
        var resultJson = response.Result!.Value;
        Assert.True(resultJson.GetProperty("isError").GetBoolean());
        Assert.Contains("boom", resultJson.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task ToolCallSpanHasServerNameAndGenAiSystem()
    {
        using var collector = new ActivityCollector("Qyl.Agents");

        var args = JsonDocument.Parse("""{"a": 1, "b": 2}""").RootElement;
        var request = MakeToolCallRequest("add", args);
        await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        var span = collector.FindSingle("execute_tool add");
        span.AssertTag("server.name", "calc-server");
        span.AssertTag("gen_ai.system", "mcp");
    }

    [Fact]
    public async Task TransportSpanHasMcpMethodAndRequestId()
    {
        using var collector = new ActivityCollector("Qyl.Agents");

        var args = JsonDocument.Parse("""{"a": 1, "b": 2}""").RootElement;
        var request = MakeToolCallRequest("add", args);
        await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        var transportSpans = collector.Where("tools/call");
        Assert.NotEmpty(transportSpans);
        var transport = transportSpans[0];
        transport.AssertTag("mcp.method.name", "tools/call");
        transport.AssertHasTag("jsonrpc.request.id");
        transport.AssertKind(ActivityKind.Server);
    }

    private static JsonRpcRequest MakeRequest(string method)
    {
        return new JsonRpcRequest
        {
            Id = JsonDocument.Parse("1").RootElement,
            Method = method
        };
    }

    private static JsonRpcRequest MakeToolCallRequest(string toolName, JsonElement args)
    {
        var json = $"{{\"name\": \"{toolName}\", \"arguments\": {args}}}";
        return new JsonRpcRequest
        {
            Id = JsonDocument.Parse("1").RootElement,
            Method = "tools/call",
            Params = JsonDocument.Parse(json).RootElement
        };
    }
}
