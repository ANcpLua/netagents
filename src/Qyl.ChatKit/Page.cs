using System.Text.Json.Serialization;

namespace Qyl.ChatKit;

/// <summary>Paginated collection of records returned from the API.</summary>
public sealed record Page<T>
{
    [JsonPropertyName("data")]
    public IReadOnlyList<T> Data { get; init; } = [];

    [JsonPropertyName("has_more")]
    public bool HasMore { get; init; }

    [JsonPropertyName("after")]
    public string? After { get; init; }
}
