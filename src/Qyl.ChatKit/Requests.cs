using System.Text.Json.Serialization;

namespace Qyl.ChatKit;

/// <summary>Base class for all request payloads.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ThreadsGetByIdReq), "threads.get_by_id")]
[JsonDerivedType(typeof(ThreadsCreateReq), "threads.create")]
[JsonDerivedType(typeof(ThreadsListReq), "threads.list")]
[JsonDerivedType(typeof(ThreadsAddUserMessageReq), "threads.add_user_message")]
[JsonDerivedType(typeof(ThreadsAddClientToolOutputReq), "threads.add_client_tool_output")]
[JsonDerivedType(typeof(ThreadsCustomActionReq), "threads.custom_action")]
[JsonDerivedType(typeof(ThreadsSyncCustomActionReq), "threads.sync_custom_action")]
[JsonDerivedType(typeof(ThreadsRetryAfterItemReq), "threads.retry_after_item")]
[JsonDerivedType(typeof(ItemsFeedbackReq), "items.feedback")]
[JsonDerivedType(typeof(AttachmentsDeleteReq), "attachments.delete")]
[JsonDerivedType(typeof(AttachmentsCreateReq), "attachments.create")]
[JsonDerivedType(typeof(InputTranscribeReq), "input.transcribe")]
[JsonDerivedType(typeof(ItemsListReq), "items.list")]
[JsonDerivedType(typeof(ThreadsUpdateReq), "threads.update")]
[JsonDerivedType(typeof(ThreadsDeleteReq), "threads.delete")]
public abstract record ChatKitRequest
{
    /// <summary>Arbitrary integration-specific metadata.</summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object?> Metadata { get; init; } = new();

    /// <summary>Return true if the given request should be processed as streaming.</summary>
    public static bool IsStreamingRequest(ChatKitRequest request) => request is
        ThreadsCreateReq or
        ThreadsAddUserMessageReq or
        ThreadsRetryAfterItemReq or
        ThreadsAddClientToolOutputReq or
        ThreadsCustomActionReq;
}

// -- Parameter records --

/// <summary>Parameters for retrieving a thread by id.</summary>
public sealed record ThreadGetByIdParams
{
    [JsonPropertyName("thread_id")]
    public required string ThreadId { get; init; }
}

/// <summary>User input required to create a thread.</summary>
public sealed record ThreadCreateParams
{
    [JsonPropertyName("input")]
    public required UserMessageInput Input { get; init; }
}

/// <summary>Pagination parameters for listing threads.</summary>
public sealed record ThreadListParams
{
    [JsonPropertyName("limit")]
    public int? Limit { get; init; }

    [JsonPropertyName("order")]
    public string Order { get; init; } = "desc";

    [JsonPropertyName("after")]
    public string? After { get; init; }
}

/// <summary>Parameters for adding a user message to a thread.</summary>
public sealed record ThreadAddUserMessageParams
{
    [JsonPropertyName("input")]
    public required UserMessageInput Input { get; init; }

    [JsonPropertyName("thread_id")]
    public required string ThreadId { get; init; }
}

/// <summary>Parameters for recording tool output in a thread.</summary>
public sealed record ThreadAddClientToolOutputParams
{
    [JsonPropertyName("thread_id")]
    public required string ThreadId { get; init; }

    [JsonPropertyName("result")]
    public object? Result { get; init; }
}

/// <summary>Parameters describing the custom action to execute.</summary>
public sealed record ThreadCustomActionParams
{
    [JsonPropertyName("thread_id")]
    public required string ThreadId { get; init; }

    [JsonPropertyName("item_id")]
    public string? ItemId { get; init; }

    [JsonPropertyName("action")]
    public required Action<string, object?> Action { get; init; }
}

/// <summary>Parameters specifying which item to retry.</summary>
public sealed record ThreadRetryAfterItemParams
{
    [JsonPropertyName("thread_id")]
    public required string ThreadId { get; init; }

    [JsonPropertyName("item_id")]
    public required string ItemId { get; init; }
}

/// <summary>Parameters describing feedback targets and sentiment.</summary>
public sealed record ItemFeedbackParams
{
    [JsonPropertyName("thread_id")]
    public required string ThreadId { get; init; }

    [JsonPropertyName("item_ids")]
    public required IReadOnlyList<string> ItemIds { get; init; }

    [JsonPropertyName("kind")]
    public required FeedbackKind Kind { get; init; }
}

/// <summary>Parameters identifying an attachment to delete.</summary>
public sealed record AttachmentDeleteParams
{
    [JsonPropertyName("attachment_id")]
    public required string AttachmentId { get; init; }
}

/// <summary>Parameters for speech transcription.</summary>
public sealed record InputTranscribeParams
{
    /// <summary>Base64-encoded audio bytes.</summary>
    [JsonPropertyName("audio_base64")]
    public required string AudioBase64 { get; init; }

    /// <summary>Raw MIME type for the audio payload, e.g. "audio/webm;codecs=opus".</summary>
    [JsonPropertyName("mime_type")]
    public required string MimeType { get; init; }
}

/// <summary>Pagination parameters for listing thread items.</summary>
public sealed record ItemsListParams
{
    [JsonPropertyName("thread_id")]
    public required string ThreadId { get; init; }

    [JsonPropertyName("limit")]
    public int? Limit { get; init; }

    [JsonPropertyName("order")]
    public string Order { get; init; } = "desc";

    [JsonPropertyName("after")]
    public string? After { get; init; }
}

/// <summary>Parameters for updating a thread's properties.</summary>
public sealed record ThreadUpdateParams
{
    [JsonPropertyName("thread_id")]
    public required string ThreadId { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }
}

/// <summary>Parameters identifying a thread to delete.</summary>
public sealed record ThreadDeleteParams
{
    [JsonPropertyName("thread_id")]
    public required string ThreadId { get; init; }
}

// -- Request types --

/// <summary>Request to fetch a single thread by its identifier.</summary>
public sealed record ThreadsGetByIdReq : ChatKitRequest
{
    [JsonPropertyName("params")]
    public required ThreadGetByIdParams Params { get; init; }
}

/// <summary>Request to create a new thread from a user message.</summary>
public sealed record ThreadsCreateReq : ChatKitRequest
{
    [JsonPropertyName("params")]
    public required ThreadCreateParams Params { get; init; }
}

/// <summary>Request to list threads.</summary>
public sealed record ThreadsListReq : ChatKitRequest
{
    [JsonPropertyName("params")]
    public required ThreadListParams Params { get; init; }
}

/// <summary>Request to append a user message to a thread.</summary>
public sealed record ThreadsAddUserMessageReq : ChatKitRequest
{
    [JsonPropertyName("params")]
    public required ThreadAddUserMessageParams Params { get; init; }
}

/// <summary>Request to add a client tool's output to a thread.</summary>
public sealed record ThreadsAddClientToolOutputReq : ChatKitRequest
{
    [JsonPropertyName("params")]
    public required ThreadAddClientToolOutputParams Params { get; init; }
}

/// <summary>Request to execute a custom action within a thread.</summary>
public sealed record ThreadsCustomActionReq : ChatKitRequest
{
    [JsonPropertyName("params")]
    public required ThreadCustomActionParams Params { get; init; }
}

/// <summary>Request to execute a custom action and return a single item update.</summary>
public sealed record ThreadsSyncCustomActionReq : ChatKitRequest
{
    [JsonPropertyName("params")]
    public required ThreadCustomActionParams Params { get; init; }
}

/// <summary>Request to retry processing after a specific thread item.</summary>
public sealed record ThreadsRetryAfterItemReq : ChatKitRequest
{
    [JsonPropertyName("params")]
    public required ThreadRetryAfterItemParams Params { get; init; }
}

/// <summary>Request to submit feedback on specific items.</summary>
public sealed record ItemsFeedbackReq : ChatKitRequest
{
    [JsonPropertyName("params")]
    public required ItemFeedbackParams Params { get; init; }
}

/// <summary>Request to remove an attachment.</summary>
public sealed record AttachmentsDeleteReq : ChatKitRequest
{
    [JsonPropertyName("params")]
    public required AttachmentDeleteParams Params { get; init; }
}

/// <summary>Request to register a new attachment.</summary>
public sealed record AttachmentsCreateReq : ChatKitRequest
{
    [JsonPropertyName("params")]
    public required AttachmentCreateParams Params { get; init; }
}

/// <summary>Request to transcribe an audio payload into text.</summary>
public sealed record InputTranscribeReq : ChatKitRequest
{
    [JsonPropertyName("params")]
    public required InputTranscribeParams Params { get; init; }
}

/// <summary>Request to list items inside a thread.</summary>
public sealed record ItemsListReq : ChatKitRequest
{
    [JsonPropertyName("params")]
    public required ItemsListParams Params { get; init; }
}

/// <summary>Request to update thread metadata.</summary>
public sealed record ThreadsUpdateReq : ChatKitRequest
{
    [JsonPropertyName("params")]
    public required ThreadUpdateParams Params { get; init; }
}

/// <summary>Request to delete a thread.</summary>
public sealed record ThreadsDeleteReq : ChatKitRequest
{
    [JsonPropertyName("params")]
    public required ThreadDeleteParams Params { get; init; }
}
