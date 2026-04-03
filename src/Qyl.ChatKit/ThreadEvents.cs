using System.Text.Json.Serialization;

namespace Qyl.ChatKit;

/// <summary>Union of all streaming events emitted to clients.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ThreadCreatedEvent), "thread.created")]
[JsonDerivedType(typeof(ThreadUpdatedEvent), "thread.updated")]
[JsonDerivedType(typeof(ThreadItemAddedEvent), "thread.item.added")]
[JsonDerivedType(typeof(ThreadItemUpdatedEvent), "thread.item.updated")]
[JsonDerivedType(typeof(ThreadItemDoneEvent), "thread.item.done")]
[JsonDerivedType(typeof(ThreadItemRemovedEvent), "thread.item.removed")]
[JsonDerivedType(typeof(ThreadItemReplacedEvent), "thread.item.replaced")]
[JsonDerivedType(typeof(StreamOptionsEvent), "stream_options")]
[JsonDerivedType(typeof(ProgressUpdateEvent), "progress_update")]
[JsonDerivedType(typeof(ClientEffectEvent), "client_effect")]
[JsonDerivedType(typeof(ErrorEvent), "error")]
[JsonDerivedType(typeof(NoticeEvent), "notice")]
public abstract record ThreadStreamEvent;

/// <summary>Event emitted when a thread is created.</summary>
public sealed record ThreadCreatedEvent : ThreadStreamEvent
{
    [JsonPropertyName("thread")]
    public required Thread Thread { get; init; }
}

/// <summary>Event emitted when a thread is updated.</summary>
public sealed record ThreadUpdatedEvent : ThreadStreamEvent
{
    [JsonPropertyName("thread")]
    public required Thread Thread { get; init; }
}

/// <summary>Event emitted when a new item is added to a thread.</summary>
public sealed record ThreadItemAddedEvent : ThreadStreamEvent
{
    [JsonPropertyName("item")]
    public required ThreadItem Item { get; init; }
}

/// <summary>Event describing an update to an existing thread item.</summary>
public sealed record ThreadItemUpdatedEvent : ThreadStreamEvent
{
    [JsonPropertyName("item_id")]
    public required string ItemId { get; init; }

    [JsonPropertyName("update")]
    public required ThreadItemUpdate Update { get; init; }
}

/// <summary>Event emitted when a thread item is marked complete.</summary>
public sealed record ThreadItemDoneEvent : ThreadStreamEvent
{
    [JsonPropertyName("item")]
    public required ThreadItem Item { get; init; }
}

/// <summary>Event emitted when a thread item is removed.</summary>
public sealed record ThreadItemRemovedEvent : ThreadStreamEvent
{
    [JsonPropertyName("item_id")]
    public required string ItemId { get; init; }
}

/// <summary>Event emitted when a thread item is replaced.</summary>
public sealed record ThreadItemReplacedEvent : ThreadStreamEvent
{
    [JsonPropertyName("item")]
    public required ThreadItem Item { get; init; }
}

/// <summary>Settings that control runtime stream behavior.</summary>
public sealed record StreamOptions
{
    /// <summary>Allow the client to request cancellation mid-stream.</summary>
    [JsonPropertyName("allow_cancel")]
    public required bool AllowCancel { get; init; }
}

/// <summary>Event emitted to set stream options at runtime.</summary>
public sealed record StreamOptionsEvent : ThreadStreamEvent
{
    [JsonPropertyName("stream_options")]
    public required StreamOptions StreamOptions { get; init; }
}

/// <summary>Event providing incremental progress from the assistant.</summary>
public sealed record ProgressUpdateEvent : ThreadStreamEvent
{
    /// <summary>Icon name. Accepts <c>vendor:*</c> and <c>lucide:*</c> prefixes.</summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

/// <summary>Event emitted to trigger a client side-effect.</summary>
public sealed record ClientEffectEvent : ThreadStreamEvent
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("data")]
    public Dictionary<string, object?> Data { get; init; } = new();
}

/// <summary>Event indicating an error occurred while processing a thread.</summary>
public sealed record ErrorEvent : ThreadStreamEvent
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = "custom";

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("allow_retry")]
    public bool AllowRetry { get; init; }
}

/// <summary>Event conveying a user-facing notice.</summary>
public sealed record NoticeEvent : ThreadStreamEvent
{
    /// <summary>Severity level: "info", "warning", or "danger".</summary>
    [JsonPropertyName("level")]
    public required string Level { get; init; }

    /// <summary>Supports markdown.</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }
}
