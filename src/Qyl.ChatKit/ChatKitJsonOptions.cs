using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.ChatKit;

/// <summary>Shared JSON serializer options for the ChatKit wire protocol.</summary>
internal static class ChatKitJsonOptions
{
    public static JsonSerializerOptions Default { get; } = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
