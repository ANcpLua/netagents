using System.Text.Json.Serialization;

namespace Qyl.ChatKit;

/// <summary>Metadata describing a thread without its items.</summary>
public record ThreadMetadata
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTime CreatedAt { get; init; }

    [JsonPropertyName("status")]
    public ThreadStatus Status { get; init; } = new ActiveStatus();

    [JsonPropertyName("allowed_image_domains")]
    public IReadOnlyList<string>? AllowedImageDomains { get; init; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object?> Metadata { get; init; } = new();
}

/// <summary>Thread with its paginated items.</summary>
public sealed record Thread : ThreadMetadata
{
    [JsonPropertyName("items")]
    public required Page<ThreadItem> Items { get; init; }
}

/// <summary>Union of lifecycle states for a thread.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ActiveStatus), "active")]
[JsonDerivedType(typeof(LockedStatus), "locked")]
[JsonDerivedType(typeof(ClosedStatus), "closed")]
public abstract record ThreadStatus;

/// <summary>Status indicating the thread is active.</summary>
public sealed record ActiveStatus : ThreadStatus;

/// <summary>Status indicating the thread is locked.</summary>
public sealed record LockedStatus : ThreadStatus
{
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

/// <summary>Status indicating the thread is closed.</summary>
public sealed record ClosedStatus : ThreadStatus
{
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}
