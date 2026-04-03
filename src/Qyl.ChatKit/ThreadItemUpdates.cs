using System.Text.Json.Serialization;
using Qyl.ChatKit.Widgets;

namespace Qyl.ChatKit;

/// <summary>Union of possible updates applied to thread items.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AssistantMessageContentPartAdded), "assistant_message.content_part.added")]
[JsonDerivedType(typeof(AssistantMessageContentPartTextDelta), "assistant_message.content_part.text_delta")]
[JsonDerivedType(typeof(AssistantMessageContentPartAnnotationAdded), "assistant_message.content_part.annotation_added")]
[JsonDerivedType(typeof(AssistantMessageContentPartDone), "assistant_message.content_part.done")]
[JsonDerivedType(typeof(WidgetStreamingTextValueDelta), "widget.streaming_text.value_delta")]
[JsonDerivedType(typeof(WidgetRootUpdated), "widget.root.updated")]
[JsonDerivedType(typeof(WidgetComponentUpdated), "widget.component.updated")]
[JsonDerivedType(typeof(WorkflowTaskAdded), "workflow.task.added")]
[JsonDerivedType(typeof(WorkflowTaskUpdated), "workflow.task.updated")]
[JsonDerivedType(typeof(GeneratedImageUpdated), "generated_image.updated")]
public abstract record ThreadItemUpdate;

/// <summary>Event emitted when new assistant content is appended.</summary>
public sealed record AssistantMessageContentPartAdded : ThreadItemUpdate
{
    [JsonPropertyName("content_index")]
    public required int ContentIndex { get; init; }

    [JsonPropertyName("content")]
    public required AssistantMessageContent Content { get; init; }
}

/// <summary>Event carrying incremental assistant text output.</summary>
public sealed record AssistantMessageContentPartTextDelta : ThreadItemUpdate
{
    [JsonPropertyName("content_index")]
    public required int ContentIndex { get; init; }

    [JsonPropertyName("delta")]
    public required string Delta { get; init; }
}

/// <summary>Event announcing a new annotation on assistant content.</summary>
public sealed record AssistantMessageContentPartAnnotationAdded : ThreadItemUpdate
{
    [JsonPropertyName("content_index")]
    public required int ContentIndex { get; init; }

    [JsonPropertyName("annotation_index")]
    public required int AnnotationIndex { get; init; }

    [JsonPropertyName("annotation")]
    public required Annotation Annotation { get; init; }
}

/// <summary>Event indicating an assistant content part is finalized.</summary>
public sealed record AssistantMessageContentPartDone : ThreadItemUpdate
{
    [JsonPropertyName("content_index")]
    public required int ContentIndex { get; init; }

    [JsonPropertyName("content")]
    public required AssistantMessageContent Content { get; init; }
}

/// <summary>Event streaming widget text deltas.</summary>
public sealed record WidgetStreamingTextValueDelta : ThreadItemUpdate
{
    [JsonPropertyName("component_id")]
    public required string ComponentId { get; init; }

    [JsonPropertyName("delta")]
    public required string Delta { get; init; }

    [JsonPropertyName("done")]
    public required bool Done { get; init; }
}

/// <summary>Event published when the widget root changes.</summary>
public sealed record WidgetRootUpdated : ThreadItemUpdate
{
    [JsonPropertyName("widget")]
    public required WidgetRoot Widget { get; init; }
}

/// <summary>Event emitted when a widget component updates.</summary>
public sealed record WidgetComponentUpdated : ThreadItemUpdate
{
    [JsonPropertyName("component_id")]
    public required string ComponentId { get; init; }

    [JsonPropertyName("component")]
    public required WidgetComponentBase Component { get; init; }
}

/// <summary>Event emitted when a workflow task is added.</summary>
public sealed record WorkflowTaskAdded : ThreadItemUpdate
{
    [JsonPropertyName("task_index")]
    public required int TaskIndex { get; init; }

    [JsonPropertyName("task")]
    public required ChatKitTask Task { get; init; }
}

/// <summary>Event emitted when a workflow task is updated.</summary>
public sealed record WorkflowTaskUpdated : ThreadItemUpdate
{
    [JsonPropertyName("task_index")]
    public required int TaskIndex { get; init; }

    [JsonPropertyName("task")]
    public required ChatKitTask Task { get; init; }
}

/// <summary>Event emitted when a generated image is updated.</summary>
public sealed record GeneratedImageUpdated : ThreadItemUpdate
{
    [JsonPropertyName("image")]
    public required GeneratedImage Image { get; init; }

    [JsonPropertyName("progress")]
    public double? Progress { get; init; }
}
