using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.ChatKit.Widgets;

/// <summary>Versatile container used for structuring widget content.</summary>
public sealed record Card : WidgetRoot
{
    [JsonPropertyName("asForm")]
    public bool? AsForm { get; init; }

    [JsonPropertyName("children")]
    public required IReadOnlyList<WidgetComponentBase> Children { get; init; }

    [JsonPropertyName("background")]
    public object? Background { get; init; }

    [JsonPropertyName("size")]
    public string? Size { get; init; }

    [JsonPropertyName("padding")]
    public object? Padding { get; init; }

    [JsonPropertyName("status")]
    public object? Status { get; init; }

    [JsonPropertyName("collapsed")]
    public bool? Collapsed { get; init; }

    [JsonPropertyName("confirm")]
    public CardAction? Confirm { get; init; }

    [JsonPropertyName("cancel")]
    public CardAction? Cancel { get; init; }

    [JsonPropertyName("theme")]
    public string? Theme { get; init; }
}

/// <summary>Container for rendering collections of list items.</summary>
public sealed record ListView : WidgetRoot
{
    [JsonPropertyName("children")]
    public required IReadOnlyList<ListViewItem> Children { get; init; }

    [JsonPropertyName("limit")]
    public object? Limit { get; init; }

    [JsonPropertyName("status")]
    public object? Status { get; init; }

    [JsonPropertyName("theme")]
    public string? Theme { get; init; }
}

/// <summary>Layout root capable of nesting components or other roots.</summary>
public sealed record BasicRoot : WidgetRoot
{
    [JsonPropertyName("children")]
    public object? Children { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// Widget component with a statically defined base shape but dynamically
/// defined additional fields loaded from a widget template or JSON schema.
/// </summary>
public sealed record DynamicWidgetComponent : WidgetComponentBase
{
    [JsonPropertyName("children")]
    public object? Children { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
