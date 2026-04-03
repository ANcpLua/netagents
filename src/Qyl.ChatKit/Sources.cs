using System.Text.Json.Serialization;

namespace Qyl.ChatKit;

/// <summary>Base class for sources displayed to users.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(FileSource), "file")]
[JsonDerivedType(typeof(UrlSource), "url")]
[JsonDerivedType(typeof(EntitySource), "entity")]
public abstract record SourceBase
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; init; }

    [JsonPropertyName("group")]
    public string? Group { get; init; }
}

/// <summary>Source metadata for file-based references.</summary>
public sealed record FileSource : SourceBase
{
    [JsonPropertyName("filename")]
    public required string Filename { get; init; }
}

/// <summary>Source metadata for external URLs.</summary>
public sealed record UrlSource : SourceBase
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("attribution")]
    public string? Attribution { get; init; }
}

/// <summary>Source metadata for entity references.</summary>
public sealed record EntitySource : SourceBase
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Icon name. Accepts <c>vendor:*</c> and <c>lucide:*</c> prefixes.</summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    /// <summary>
    /// Optional label shown with the icon in the default entity hover header
    /// when no preview callback is provided.
    /// </summary>
    [JsonPropertyName("label")]
    public string? Label { get; init; }

    /// <summary>
    /// Optional label for the inline annotation view. When not provided, the icon is used instead.
    /// </summary>
    [JsonPropertyName("inline_label")]
    public string? InlineLabel { get; init; }

    /// <summary>Per-entity toggle to wire client callbacks and render this entity as interactive.</summary>
    [JsonPropertyName("interactive")]
    public bool Interactive { get; init; }

    /// <summary>Additional data for the entity source that is passed to client entity callbacks.</summary>
    [JsonPropertyName("data")]
    public Dictionary<string, object?> Data { get; init; } = new();
}
