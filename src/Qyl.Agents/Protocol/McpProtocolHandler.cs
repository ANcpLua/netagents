namespace Qyl.Agents.Protocol;

using System.Diagnostics;
using System.Text.Json;
using Tasks;

internal sealed class McpProtocolHandler<TServer>(TServer server, IMcpTaskStore? taskStore = null)
    where TServer : class, IMcpServer
{
    private static readonly McpServerInfo s_info = TServer.GetServerInfo();
    private static readonly IReadOnlyList<McpToolInfo> s_tools = TServer.GetToolInfos();
    private static readonly IReadOnlyList<McpResourceInfo> s_resources = TServer.GetResourceInfos();
    private static readonly IReadOnlyList<McpPromptInfo> s_prompts = TServer.GetPromptInfos();

    // Cached JSON allocations — Clone() detaches from JsonDocument lifetime
    private static readonly JsonElement s_emptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    private static readonly JsonElement s_defaultSchema =
        JsonDocument.Parse("""{"type":"object"}""").RootElement.Clone();

    // Pre-parsed tool schemas (computed once at construction)
    private static readonly JsonElement[] s_toolSchemas = ParseToolSchemas();

    // Whether any tool declares task support (drives capabilities advertisement)
    private static readonly bool s_hasTaskSupport = HasAnyTaskSupport();

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
            "resources/list" => HandleResourcesList(request),
            "resources/read" => await HandleResourcesReadAsync(request, ct),
            "prompts/list" => HandlePromptsList(request),
            "prompts/get" => await HandlePromptsGetAsync(request, ct),
            "tasks/list" => await HandleTasksListAsync(request, ct),
            "tasks/get" => await HandleTasksGetAsync(request, ct),
            "tasks/cancel" => await HandleTasksCancelAsync(request, ct),
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

    private JsonRpcResponse HandleInitialize(JsonRpcRequest request)
    {
        return SuccessResponse(request.Id, BuildInitializeResult());
    }

    private JsonElement BuildInitializeResult()
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
            w.WriteStartObject("resources");
            w.WriteBoolean("listChanged", false);
            w.WriteEndObject();
            w.WriteStartObject("prompts");
            w.WriteBoolean("listChanged", false);
            w.WriteEndObject();
            if (taskStore is not null && s_hasTaskSupport)
            {
                w.WriteStartObject("tasks");
                w.WriteEndObject();
            }

            // Extensions — empty object signals support for future extensibility
            w.WriteStartObject("extensions");
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
                if (tool.ReadOnlyHint is not null || tool.DestructiveHint is not null ||
                    tool.IdempotentHint is not null || tool.OpenWorldHint is not null)
                {
                    w.WriteStartObject("annotations");
                    if (tool.ReadOnlyHint is { } ro)
                        w.WriteBoolean("readOnlyHint", ro);
                    if (tool.DestructiveHint is { } dest)
                        w.WriteBoolean("destructiveHint", dest);
                    if (tool.IdempotentHint is { } idem)
                        w.WriteBoolean("idempotentHint", idem);
                    if (tool.OpenWorldHint is { } ow)
                        w.WriteBoolean("openWorldHint", ow);
                    w.WriteEndObject();
                }

                if (tool.TaskSupport is not ToolTaskSupport.Unset)
                {
                    w.WriteStartObject("execution");
                    w.WriteString("taskSupport", tool.TaskSupport switch
                    {
                        ToolTaskSupport.Forbidden => "forbidden",
                        ToolTaskSupport.Optional => "optional",
                        ToolTaskSupport.Required => "required",
                        _ => "forbidden"
                    });
                    w.WriteEndObject();
                }

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

    private static JsonRpcResponse HandleResourcesList(JsonRpcRequest request)
    {
        return SuccessResponse(request.Id, BuildResourcesListResult());
    }

    private static JsonElement BuildResourcesListResult()
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteStartArray("resources");
            foreach (var resource in s_resources)
            {
                w.WriteStartObject();
                w.WriteString("uri", resource.Uri);
                if (resource.MimeType is not null)
                    w.WriteString("mimeType", resource.MimeType);
                if (resource.Description is not null)
                    w.WriteString("description", resource.Description);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return JsonDocument.Parse(ms.ToArray()).RootElement.Clone();
    }

    private async Task<JsonRpcResponse> HandleResourcesReadAsync(
        JsonRpcRequest request, CancellationToken ct)
    {
        if (request.Params is not { } p)
            return ErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Missing params");

        if (!p.TryGetProperty("uri", out var uriEl) || uriEl.ValueKind != JsonValueKind.String)
            return ErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Missing params.uri");

        var uri = uriEl.GetString()!;

        try
        {
            var result = await server.DispatchResourceReadAsync(uri, ct);
            return SuccessResponse(request.Id, BuildResourceReadResult(uri, result));
        }
        catch (ArgumentException ex)
        {
            // Unknown URI — generated dispatch throws ArgumentException
            return ErrorResponse(request.Id, McpErrorCodes.InvalidParams, ex.Message);
        }
    }

    private static JsonElement BuildResourceReadResult(string uri, ResourceReadResult result)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteStartArray("contents");
            w.WriteStartObject();
            w.WriteString("uri", uri);
            if (result.IsBinary)
                w.WriteString("blob", result.Content);
            else
                w.WriteString("text", result.Content);
            w.WriteEndObject();
            w.WriteEndArray();
            w.WriteEndObject();
        }

        return JsonDocument.Parse(ms.ToArray()).RootElement.Clone();
    }

    private static JsonRpcResponse HandlePromptsList(JsonRpcRequest request)
    {
        return SuccessResponse(request.Id, BuildPromptsListResult());
    }

    private static JsonElement BuildPromptsListResult()
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteStartArray("prompts");
            foreach (var prompt in s_prompts)
            {
                w.WriteStartObject();
                w.WriteString("name", prompt.Name);
                if (prompt.Description is not null)
                    w.WriteString("description", prompt.Description);
                if (prompt.Arguments.Count > 0)
                {
                    w.WriteStartArray("arguments");
                    foreach (var arg in prompt.Arguments)
                    {
                        w.WriteStartObject();
                        w.WriteString("name", arg.Name);
                        if (arg.Description is not null)
                            w.WriteString("description", arg.Description);
                        w.WriteBoolean("required", arg.Required);
                        w.WriteEndObject();
                    }

                    w.WriteEndArray();
                }

                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return JsonDocument.Parse(ms.ToArray()).RootElement.Clone();
    }

    private async Task<JsonRpcResponse> HandlePromptsGetAsync(
        JsonRpcRequest request, CancellationToken ct)
    {
        if (request.Params is not { } p)
            return ErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Missing params");

        if (!p.TryGetProperty("name", out var nameEl) || nameEl.ValueKind != JsonValueKind.String)
            return ErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Missing params.name");

        var promptName = nameEl.GetString()!;

        var arguments = p.TryGetProperty("arguments", out var argsEl)
            ? argsEl
            : s_emptyObject;

        try
        {
            var result = await server.DispatchPromptAsync(promptName, arguments, ct);
            return SuccessResponse(request.Id, BuildPromptGetResult(result));
        }
        catch (ArgumentException ex)
        {
            // Unknown prompt name — generated dispatch throws ArgumentException
            return ErrorResponse(request.Id, McpErrorCodes.InvalidParams, ex.Message);
        }
    }

    private static JsonElement BuildPromptGetResult(PromptResult result)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            if (result.Description is not null)
                w.WriteString("description", result.Description);
            w.WriteStartArray("messages");
            foreach (var message in result.Messages)
            {
                w.WriteStartObject();
                w.WriteString("role", message.Role);
                w.WriteStartObject("content");
                w.WriteString("type", "text");
                w.WriteString("text", message.Content);
                w.WriteEndObject();
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return JsonDocument.Parse(ms.ToArray()).RootElement.Clone();
    }

    // --- Tasks ---

    private async Task<JsonRpcResponse> HandleTasksListAsync(JsonRpcRequest request, CancellationToken ct)
    {
        if (taskStore is null)
            return ErrorResponse(request.Id, McpErrorCodes.MethodNotFound, "Tasks not enabled");

        var tasks = await taskStore.ListAsync(ct: ct);
        return SuccessResponse(request.Id, BuildTasksListResult(tasks));
    }

    private async Task<JsonRpcResponse> HandleTasksGetAsync(JsonRpcRequest request, CancellationToken ct)
    {
        if (taskStore is null)
            return ErrorResponse(request.Id, McpErrorCodes.MethodNotFound, "Tasks not enabled");

        if (request.Params is not { } p)
            return ErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Missing params");

        if (!p.TryGetProperty("taskId", out var idEl) || idEl.ValueKind != JsonValueKind.String)
            return ErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Missing params.taskId");

        var task = await taskStore.GetAsync(idEl.GetString()!, ct);
        if (task is null)
            return ErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Task not found");

        return SuccessResponse(request.Id, BuildTaskJson(task));
    }

    private async Task<JsonRpcResponse> HandleTasksCancelAsync(JsonRpcRequest request, CancellationToken ct)
    {
        if (taskStore is null)
            return ErrorResponse(request.Id, McpErrorCodes.MethodNotFound, "Tasks not enabled");

        if (request.Params is not { } p)
            return ErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Missing params");

        if (!p.TryGetProperty("taskId", out var idEl) || idEl.ValueKind != JsonValueKind.String)
            return ErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Missing params.taskId");

        var cancelled = await taskStore.CancelAsync(idEl.GetString()!, ct);
        if (!cancelled)
            return ErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Task not found or already terminal");

        var task = await taskStore.GetAsync(idEl.GetString()!, ct);
        return SuccessResponse(request.Id, task is not null ? BuildTaskJson(task) : s_emptyObject);
    }

    private static JsonElement BuildTasksListResult(IReadOnlyList<McpTask> tasks)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteStartArray("tasks");
            foreach (var task in tasks)
                WriteTaskObject(w, task);
            w.WriteEndArray();
            w.WriteEndObject();
        }

        return JsonDocument.Parse(ms.ToArray()).RootElement.Clone();
    }

    private static JsonElement BuildTaskJson(McpTask task)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
            WriteTaskObject(w, task);
        return JsonDocument.Parse(ms.ToArray()).RootElement.Clone();
    }

    private static void WriteTaskObject(Utf8JsonWriter w, McpTask task)
    {
        w.WriteStartObject();
        w.WriteString("taskId", task.TaskId);
        w.WriteString("status", task.Status switch
        {
            McpTaskStatus.Working => "working",
            McpTaskStatus.InputRequired => "input_required",
            McpTaskStatus.Completed => "completed",
            McpTaskStatus.Failed => "failed",
            McpTaskStatus.Cancelled => "cancelled",
            _ => "working"
        });
        if (task.StatusMessage is not null)
            w.WriteString("statusMessage", task.StatusMessage);
        w.WriteString("createdAt", task.CreatedAt);
        w.WriteString("lastUpdatedAt", task.LastUpdatedAt);
        if (task.TimeToLive is { } ttl)
            w.WriteNumber("ttl", (long)ttl.TotalMilliseconds);
        if (task.PollInterval is { } pi)
            w.WriteNumber("pollInterval", (long)pi.TotalMilliseconds);
        w.WriteEndObject();
    }

    // --- Helpers ---

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

    private static bool HasAnyTaskSupport()
    {
        foreach (var tool in s_tools)
            if (tool.TaskSupport is not ToolTaskSupport.Unset)
                return true;
        return false;
    }
}
