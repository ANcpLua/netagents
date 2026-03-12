using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Agents.Protocol;

internal sealed class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")] public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")] public JsonElement? Id { get; set; }

    [JsonPropertyName("method")] public string Method { get; set; } = "";

    [JsonPropertyName("params")] public JsonElement? Params { get; set; }

    public bool IsNotification => Id is null;
}

internal sealed class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")] public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("id")] public JsonElement? Id { get; init; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Result { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcError? Error { get; init; }
}

internal sealed class JsonRpcError
{
    [JsonPropertyName("code")] public int Code { get; init; }

    [JsonPropertyName("message")] public string Message { get; init; } = "";

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Data { get; init; }
}

[JsonSerializable(typeof(JsonRpcRequest))]
[JsonSerializable(typeof(JsonRpcResponse))]
[JsonSerializable(typeof(JsonRpcError))]
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class JsonRpcJsonContext : JsonSerializerContext;

internal static class McpErrorCodes
{
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
}