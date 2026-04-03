using System.Text.Json.Serialization;
using Qyl.ChatKit.Widgets;

namespace Qyl.ChatKit;

/// <summary>Base fields shared by all thread items.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(UserMessageItem), "user_message")]
[JsonDerivedType(typeof(AssistantMessageItem), "assistant_message")]
[JsonDerivedType(typeof(ClientToolCallItem), "client_tool_call")]
[JsonDerivedType(typeof(WidgetItem), "widget")]
[JsonDerivedType(typeof(GeneratedImageItem), "generated_image")]
[JsonDerivedType(typeof(WorkflowItem), "workflow")]
[JsonDerivedType(typeof(TaskItem), "task")]
[JsonDerivedType(typeof(HiddenContextItem), "hidden_context_item")]
[JsonDerivedType(typeof(SdkHiddenContextItem), "sdk_hidden_context")]
[JsonDerivedType(typeof(EndOfTurnItem), "end_of_turn")]
public abstract record ThreadItem
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("thread_id")]
    public required string ThreadId { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTime CreatedAt { get; init; }
}

/// <summary>Thread item representing a user message.</summary>
public sealed record UserMessageItem : ThreadItem
{
    [JsonPropertyName("content")]
    public required IReadOnlyList<UserMessageContent> Content { get; init; }

    [JsonPropertyName("attachments")]
    public IReadOnlyList<AttachmentBase> Attachments { get; init; } = [];

    [JsonPropertyName("quoted_text")]
    public string? QuotedText { get; init; }

    [JsonPropertyName("inference_options")]
    public required InferenceOptions InferenceOptions { get; init; }
}

/// <summary>Thread item representing an assistant message.</summary>
public sealed record AssistantMessageItem : ThreadItem
{
    [JsonPropertyName("content")]
    public required IReadOnlyList<AssistantMessageContent> Content { get; init; }
}

/// <summary>Thread item capturing a client tool call.</summary>
public sealed record ClientToolCallItem : ThreadItem
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "pending";

    [JsonPropertyName("call_id")]
    public required string CallId { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("arguments")]
    public Dictionary<string, object?> Arguments { get; init; } = new();

    [JsonPropertyName("output")]
    public object? Output { get; init; }
}

/// <summary>Thread item containing widget content.</summary>
public sealed record WidgetItem : ThreadItem
{
    [JsonPropertyName("widget")]
    public required WidgetRoot Widget { get; init; }

    [JsonPropertyName("copy_text")]
    public string? CopyText { get; init; }
}

/// <summary>Generated image metadata.</summary>
public sealed record GeneratedImage
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("url")]
    public required string Url { get; init; }
}

/// <summary>Thread item containing a generated image.</summary>
public sealed record GeneratedImageItem : ThreadItem
{
    [JsonPropertyName("image")]
    public GeneratedImage? Image { get; init; }
}

/// <summary>Thread item representing a workflow.</summary>
public sealed record WorkflowItem : ThreadItem
{
    [JsonPropertyName("workflow")]
    public required Workflow Workflow { get; init; }
}

/// <summary>Thread item containing a task.</summary>
public sealed record TaskItem : ThreadItem
{
    [JsonPropertyName("task")]
    public required ChatKitTask Task { get; init; }
}

/// <summary>Marker item indicating the assistant ends its turn.</summary>
public sealed record EndOfTurnItem : ThreadItem;

/// <summary>
/// Hidden context is never sent to the client. It is only used internally to store
/// additional context in a specific place in the thread.
/// </summary>
public sealed record HiddenContextItem : ThreadItem
{
    [JsonPropertyName("content")]
    public object? Content { get; init; }
}

/// <summary>
/// Hidden context used by the SDK for storing additional context for internal operations.
/// </summary>
public sealed record SdkHiddenContextItem : ThreadItem
{
    [JsonPropertyName("content")]
    public required string Content { get; init; }
}

/// <summary>Single thread item update returned by a sync custom action.</summary>
public sealed record SyncCustomActionResponse
{
    [JsonPropertyName("updated_item")]
    public ThreadItem? UpdatedItem { get; init; }
}
