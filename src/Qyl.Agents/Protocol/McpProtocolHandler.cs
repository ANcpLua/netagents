namespace Qyl.Agents.Protocol;

using System.Diagnostics;
using System.Text.Json;

internal sealed class McpProtocolHandler<TServer>(TServer server) where TServer : class, IMcpServer
{
    private static readonly McpServerInfo s_info = TServer.GetServerInfo();
    private static readonly IReadOnlyList<McpToolInfo> s_tools = TServer.GetToolInfos();

    // Cached JSON allocations — Clone() detaches from JsonDocument lifetime
    private static readonly JsonElement s_emptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    private static readonly JsonElement s_defaultSchema =
        JsonDocument.Parse("""{"type":"object"}""").RootElement.Clone();

    // Pre-parsed tool schemas (computed once at construction)
    private static readonly JsonElement[] s_toolSchemas = ParseToolSchemas();

    // Shared ActivitySource for transport-level spans
    private static readonly ActivitySource s_activitySource = new("Qyl.Agents",
        typeof(TServer).Assembly.GetName().Version?.ToString() ?? "0.0.0");

    public async Task<JsonRpcResponse?> HandleAsync(JsonRpcRequest request, CancellationToken ct)
    {
        if (request.IsNotification)
            return null;

        // Transport-level OTel span
        var spanName = BuildSpanName(request);
        using var activity = s_activitySource.StartActivity(spanName, ActivityKind.Server);
        if (activity is not null)
        {
            activity.SetTag("mcp.method.name", request.Method);
            if (request.Id is { } id)
                activity.SetTag("jsonrpc.request.id", id.ToString());
        }

        return request.Method switch
        {
            "initialize" => HandleInitialize(request),
            "tools/list" => HandleToolsList(request),
            "tools/call" => await HandleToolsCallAsync(request, activity, ct),
            "ping" => HandlePing(request),
            _ => ErrorResponse(request.Id, McpErrorCodes.MethodNotFound, $"Unknown method: {request.Method}")
        };
    }

    private static string BuildSpanName(JsonRpcRequest request)
    {
        if (request.Method != "tools/call" || request.Params is not { } p)
            return request.Method;

        // tools/call {tool.name} — append target when meaningful
        if (p.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
            return $"tools/call {nameEl.GetString()}";

        return "tools/call";
    }

    private static JsonRpcResponse HandleInitialize(JsonRpcRequest request)
    {
        return SuccessResponse(request.Id, BuildInitializeResult());
    }

    private static JsonElement BuildInitializeResult()
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("protocolVersion", "2024-11-05");
            w.WriteStartObject("capabilities");
            w.WriteStartObject("tools");
            w.WriteBoolean("listChanged", false);
            w.WriteEndObject();
            w.WriteEndObject();
            w.WriteStartObject("serverInfo");
            w.WriteString("name", s_info.Name);
            w.WriteString("version", s_info.Version ?? "0.0.0");
            w.WriteEndObject();
            w.WriteEndObject();
        }

        return JsonDocument.Parse(ms.ToArray()).RootElement.Clone();
    }

    private static JsonRpcResponse HandleToolsList(JsonRpcRequest request)
    {
        return SuccessResponse(request.Id, BuildToolsListResult());
    }

    private static JsonElement BuildToolsListResult()
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteStartArray("tools");
            for (var i = 0; i < s_tools.Count; i++)
            {
                var tool = s_tools[i];
                w.WriteStartObject();
                w.WriteString("name", tool.Name);
                w.WriteString("description", tool.Description ?? "");
                w.WritePropertyName("inputSchema");
                s_toolSchemas[i].WriteTo(w);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return JsonDocument.Parse(ms.ToArray()).RootElement.Clone();
    }

    private async Task<JsonRpcResponse> HandleToolsCallAsync(
        JsonRpcRequest request, Activity? activity, CancellationToken ct)
    {
        if (request.Params is not { } p)
            return ErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Missing params");

        if (!p.TryGetProperty("name", out var nameEl) || nameEl.ValueKind != JsonValueKind.String)
            return ErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Missing params.name");

        var toolName = nameEl.GetString()!;

        // Enrich transport span with tool name for tools/call
        activity?.SetTag("gen_ai.tool.name", toolName);

        var arguments = p.TryGetProperty("arguments", out var argsEl)
            ? argsEl
            : s_emptyObject;

        try
        {
            var resultJson = await server.DispatchToolCallAsync(toolName, arguments, ct);
            return SuccessResponse(request.Id, BuildToolCallResult(resultJson, false));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse(request.Id, McpErrorCodes.InvalidParams, ex.Message);
        }
        catch (Exception ex)
        {
            // MCP protocol requires tool errors to be returned as isError responses
            // rather than crashing the server (losing the transport connection).
            return SuccessResponse(request.Id, BuildToolCallResult(ex.Message, true));
        }
    }

    private static JsonElement BuildToolCallResult(string text, bool isError)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteStartArray("content");
            w.WriteStartObject();
            w.WriteString("type", "text");
            w.WriteString("text", text);
            w.WriteEndObject();
            w.WriteEndArray();
            if (isError)
                w.WriteBoolean("isError", true);
            w.WriteEndObject();
        }

        return JsonDocument.Parse(ms.ToArray()).RootElement.Clone();
    }

    private static JsonRpcResponse HandlePing(JsonRpcRequest request)
    {
        return SuccessResponse(request.Id, s_emptyObject);
    }

    private static JsonRpcResponse SuccessResponse(JsonElement? id, JsonElement result)
    {
        return new JsonRpcResponse { Id = id, Result = result };
    }

    private static JsonRpcResponse ErrorResponse(JsonElement? id, int code, string message)
    {
        return new JsonRpcResponse { Id = id, Error = new JsonRpcError { Code = code, Message = message } };
    }

    private static JsonElement[] ParseToolSchemas()
    {
        var schemas = new JsonElement[s_tools.Count];
        for (var i = 0; i < s_tools.Count; i++)
        {
            var tool = s_tools[i];
            schemas[i] = tool.InputSchema.Length > 0
                ? JsonDocument.Parse(tool.InputSchema).RootElement.Clone()
                : s_defaultSchema;
        }

        return schemas;
    }
}
