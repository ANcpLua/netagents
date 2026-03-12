using System.Diagnostics;
using System.Text.Json;

namespace Qyl.Agents.Protocol;

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
        var result = JsonSerializer.SerializeToDocument(new
        {
            protocolVersion = "2024-11-05",
            capabilities = new
            {
                tools = new { listChanged = false }
            },
            serverInfo = new
            {
                name = s_info.Name,
                version = s_info.Version ?? "0.0.0"
            }
        });

        return SuccessResponse(request.Id, result.RootElement);
    }

    private static JsonRpcResponse HandleToolsList(JsonRpcRequest request)
    {
        var tools = new List<object>(s_tools.Count);
        for (var i = 0; i < s_tools.Count; i++)
        {
            var tool = s_tools[i];
            tools.Add(new
            {
                name = tool.Name,
                description = tool.Description ?? "",
                inputSchema = s_toolSchemas[i]
            });
        }

        var result = JsonSerializer.SerializeToDocument(new { tools });
        return SuccessResponse(request.Id, result.RootElement);
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

            var content = new[]
            {
                new { type = "text", text = resultJson }
            };

            var result = JsonSerializer.SerializeToDocument(new { content });
            return SuccessResponse(request.Id, result.RootElement);
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
            var content = new[]
            {
                new { type = "text", text = ex.Message }
            };

            var result = JsonSerializer.SerializeToDocument(new { content, isError = true });
            return SuccessResponse(request.Id, result.RootElement);
        }
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