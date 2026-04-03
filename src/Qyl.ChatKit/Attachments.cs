using System.Text.Json.Serialization;

namespace Qyl.ChatKit;

/// <summary>Base metadata shared by all attachments.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(FileAttachment), "file")]
[JsonDerivedType(typeof(ImageAttachment), "image")]
public abstract record AttachmentBase
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("mime_type")]
    public required string MimeType { get; init; }

    /// <summary>
    /// Two-phase upload instructions.
    /// Should be set to null after upload is complete or when using direct upload.
    /// </summary>
    [JsonPropertyName("upload_descriptor")]
    public AttachmentUploadDescriptor? UploadDescriptor { get; init; }

    /// <summary>
    /// The thread the attachment belongs to, if any.
    /// Added when the user message that contains the attachment is saved to store.
    /// </summary>
    [JsonPropertyName("thread_id")]
    public string? ThreadId { get; init; }

    /// <summary>
    /// Integration-only metadata stored with the attachment.
    /// Ignored by ChatKit and not returned in server responses.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; init; }
}

/// <summary>Attachment representing a generic file.</summary>
public sealed record FileAttachment : AttachmentBase;

/// <summary>Attachment representing an image resource.</summary>
public sealed record ImageAttachment : AttachmentBase
{
    [JsonPropertyName("preview_url")]
    public required Uri PreviewUrl { get; init; }
}

/// <summary>Two-phase upload instructions.</summary>
public sealed record AttachmentUploadDescriptor
{
    [JsonPropertyName("url")]
    public required Uri Url { get; init; }

    /// <summary>The HTTP method to use when uploading the file for two-phase upload.</summary>
    [JsonPropertyName("method")]
    public required string Method { get; init; }

    /// <summary>Optional headers to include in the upload request.</summary>
    [JsonPropertyName("headers")]
    public Dictionary<string, string> Headers { get; init; } = new();
}

/// <summary>Metadata needed to initialize an attachment.</summary>
public sealed record AttachmentCreateParams
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("size")]
    public required int Size { get; init; }

    [JsonPropertyName("mime_type")]
    public required string MimeType { get; init; }
}

/// <summary>Audio input data for transcription.</summary>
public sealed record AudioInput
{
    /// <summary>Audio data bytes.</summary>
    [JsonPropertyName("data")]
    public required byte[] Data { get; init; }

    /// <summary>Raw MIME type for the audio payload, e.g. "audio/webm;codecs=opus".</summary>
    [JsonPropertyName("mime_type")]
    public required string MimeType { get; init; }

    /// <summary>Media type for the audio payload, e.g. "audio/webm".</summary>
    [JsonIgnore]
    public string MediaType => MimeType.Split(';', 2)[0];
}

/// <summary>Input speech transcription result.</summary>
public sealed record TranscriptionResult
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}
