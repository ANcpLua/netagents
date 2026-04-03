using System.Text.Json.Serialization;

namespace Qyl.ChatKit;

/// <summary>Payload describing a user message submission.</summary>
public sealed record UserMessageInput
{
    [JsonPropertyName("content")]
    public required IReadOnlyList<UserMessageContent> Content { get; init; }

    [JsonPropertyName("attachments")]
    public IReadOnlyList<string> Attachments { get; init; } = [];

    [JsonPropertyName("quoted_text")]
    public string? QuotedText { get; init; }

    [JsonPropertyName("inference_options")]
    public required InferenceOptions InferenceOptions { get; init; }
}

/// <summary>Polymorphic user message content payloads.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(UserMessageTextContent), "input_text")]
[JsonDerivedType(typeof(UserMessageTagContent), "input_tag")]
public abstract record UserMessageContent;

/// <summary>User message content containing plaintext.</summary>
public sealed record UserMessageTextContent : UserMessageContent
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

/// <summary>User message content representing an interactive tag.</summary>
public sealed record UserMessageTagContent : UserMessageContent
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("data")]
    public Dictionary<string, object?> Data { get; init; } = new();

    [JsonPropertyName("group")]
    public string? Group { get; init; }

    [JsonPropertyName("interactive")]
    public bool Interactive { get; init; }
}

/// <summary>Assistant message content consisting of text and annotations.</summary>
public sealed record AssistantMessageContent
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "output_text";

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("annotations")]
    public IReadOnlyList<Annotation> Annotations { get; init; } = [];
}

/// <summary>Reference to supporting context attached to assistant output.</summary>
public sealed record Annotation
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "annotation";

    [JsonPropertyName("source")]
    public required SourceBase Source { get; init; }

    [JsonPropertyName("index")]
    public int? Index { get; init; }
}

/// <summary>Model and tool configuration for message processing.</summary>
public sealed record InferenceOptions
{
    [JsonPropertyName("tool_choice")]
    public ToolChoice? ToolChoice { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }
}

/// <summary>Explicit tool selection for the assistant to invoke.</summary>
public sealed record ToolChoice
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
}
