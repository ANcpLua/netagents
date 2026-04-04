namespace Qyl.Agents.Tests;

using System.Diagnostics;
using System.Text.Json;
using ANcpLua.Roslyn.Utilities.Testing.AgentTesting;
using AwesomeAssertions;
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

        response.Should().NotBeNull();
        response!.Error.Should().BeNull();
        response.Result.Should().NotBeNull();

        var serverInfo = response.Result!.Value.GetProperty("serverInfo");
        serverInfo.GetProperty("name").GetString().Should().Be("calc-server");
    }

    [Fact]
    public async Task ToolsListReturnsAllTools()
    {
        var request = MakeRequest("tools/list");
        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        response.Should().NotBeNull();
        var tools = response!.Result!.Value.GetProperty("tools");
        tools.GetArrayLength().Should().Be(3);

        var toolNames = tools.EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()!)
            .OrderBy(n => n)
            .ToList();
        toolNames.Should().Contain("add");
        toolNames.Should().Contain("multiply");
        toolNames.Should().Contain("fail");
    }

    [Fact]
    public async Task ToolsCallAddReturnsResult()
    {
        var args = JsonDocument.Parse("""{"a": 3, "b": 4}""").RootElement;
        var request = MakeToolCallRequest("add", args);
        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        response.Should().NotBeNull();
        response!.Error.Should().BeNull();

        var content = response.Result!.Value.GetProperty("content");
        var text = content[0].GetProperty("text").GetString();
        text.Should().Be("7");
    }

    [Fact]
    public async Task ToolsCallMultiplyReturnsResult()
    {
        var args = JsonDocument.Parse("""{"a": 5, "b": 6}""").RootElement;
        var request = MakeToolCallRequest("multiply", args);
        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        response.Should().NotBeNull();
        var content = response!.Result!.Value.GetProperty("content");
        var text = content[0].GetProperty("text").GetString();
        text.Should().Be("30");
    }

    [Fact]
    public async Task ToolsCallUnknownToolReturnsError()
    {
        var args = JsonDocument.Parse("{}").RootElement;
        var request = MakeToolCallRequest("nonexistent", args);
        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        response.Should().NotBeNull();
        response!.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(McpErrorCodes.InvalidParams);
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

        skillMd.Should().Contain("---");
        skillMd.Should().Contain("name: calc-server");
        skillMd.Should().Contain("### add");
        skillMd.Should().Contain("### multiply");
        skillMd.Should().Contain("`a` (integer, required): First number");
        skillMd.Should().Contain("`b` (integer, required): Second number");
        skillMd.Should().Contain("`a` (integer, required): First factor");
        skillMd.Should().Contain("`b` (integer, required): Second factor");
    }

    [Fact]
    public async Task NotificationReturnsNull()
    {
        var request = new JsonRpcRequest { Method = "notifications/initialized" };
        var response = await _handler.HandleAsync(request, CancellationToken.None);
        response.Should().BeNull();
    }

    [Fact]
    public async Task RepeatedToolsListReturnsConsistentSchema()
    {
        var request = MakeRequest("tools/list");

        var response1 = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);
        var response2 = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        var json1 = response1!.Result!.Value.GetRawText();
        var json2 = response2!.Result!.Value.GetRawText();
        json2.Should().Be(json1);
    }

    [Fact]
    public async Task ToolExceptionReturnsIsErrorContent()
    {
        var args = JsonDocument.Parse("""{"message": "boom"}""").RootElement;
        var request = MakeToolCallRequest("fail", args);
        var response = await _handler.HandleAsync(request, TestContext.Current.CancellationToken);

        response.Should().NotBeNull();
        response!.Error.Should().BeNull();
        var resultJson = response.Result!.Value;
        resultJson.GetProperty("isError").GetBoolean().Should().BeTrue();
        resultJson.GetProperty("content")[0].GetProperty("text").GetString().Should().Contain("boom");
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
        transportSpans.Should().NotBeEmpty();
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
