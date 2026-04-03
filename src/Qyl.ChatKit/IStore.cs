using System.Text.Json.Serialization;

namespace Qyl.ChatKit;

/// <summary>Kind of item for store ID generation.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StoreItemType>))]
public enum StoreItemType
{
    [JsonStringEnumMemberName("thread")]
    Thread,

    [JsonStringEnumMemberName("message")]
    Message,

    [JsonStringEnumMemberName("tool_call")]
    ToolCall,

    [JsonStringEnumMemberName("task")]
    Task,

    [JsonStringEnumMemberName("workflow")]
    Workflow,

    [JsonStringEnumMemberName("attachment")]
    Attachment,

    [JsonStringEnumMemberName("sdk_hidden_context")]
    SdkHiddenContext
}

/// <summary>Generates prefixed identifiers for store items.</summary>
public static class StoreIdGenerator
{
    private static readonly Dictionary<StoreItemType, string> Prefixes = new()
    {
        [StoreItemType.Thread] = "thr",
        [StoreItemType.Message] = "msg",
        [StoreItemType.ToolCall] = "tc",
        [StoreItemType.Workflow] = "wf",
        [StoreItemType.Task] = "tsk",
        [StoreItemType.Attachment] = "atc",
        [StoreItemType.SdkHiddenContext] = "shcx"
    };

    public static string GenerateId(StoreItemType itemType)
    {
        var prefix = Prefixes[itemType];
        return $"{prefix}_{Guid.NewGuid():N}"[..12];
    }
}

/// <summary>Thrown when a requested entity is not found in the store.</summary>
public sealed class NotFoundException() : Exception("Entity not found");

/// <summary>Attachment-specific store operations.</summary>
public interface IAttachmentStore<in TContext>
{
    /// <summary>Delete an attachment by id.</summary>
    Task DeleteAttachmentAsync(string attachmentId, TContext context);

    /// <summary>Create an attachment record from upload metadata.</summary>
    Task<AttachmentBase> CreateAttachmentAsync(AttachmentCreateParams input, TContext context);

    /// <summary>
    /// Return a new identifier for a file. Override to customize file ID generation.
    /// </summary>
    string GenerateAttachmentId(string mimeType, TContext context) =>
        StoreIdGenerator.GenerateId(StoreItemType.Attachment);
}

/// <summary>Primary store interface for threads, items, and attachments.</summary>
public interface IStore<in TContext>
{
    /// <summary>Return a new identifier for a thread.</summary>
    string GenerateThreadId(TContext context) =>
        StoreIdGenerator.GenerateId(StoreItemType.Thread);

    /// <summary>Return a new identifier for a thread item.</summary>
    string GenerateItemId(StoreItemType itemType, ThreadMetadata thread, TContext context) =>
        StoreIdGenerator.GenerateId(itemType);

    /// <summary>Load a thread's metadata by id.</summary>
    Task<ThreadMetadata> LoadThreadAsync(string threadId, TContext context);

    /// <summary>Persist thread metadata (title, status, etc.).</summary>
    Task SaveThreadAsync(ThreadMetadata thread, TContext context);

    /// <summary>Load a page of thread items with pagination controls.</summary>
    Task<Page<ThreadItem>> LoadThreadItemsAsync(
        string threadId,
        string? after,
        int limit,
        string order,
        TContext context);

    /// <summary>Upsert attachment metadata.</summary>
    Task SaveAttachmentAsync(AttachmentBase attachment, TContext context);

    /// <summary>Load attachment metadata by id.</summary>
    Task<AttachmentBase> LoadAttachmentAsync(string attachmentId, TContext context);

    /// <summary>Delete attachment metadata by id.</summary>
    Task DeleteAttachmentAsync(string attachmentId, TContext context);

    /// <summary>Load a page of threads with pagination controls.</summary>
    Task<Page<ThreadMetadata>> LoadThreadsAsync(
        int limit,
        string? after,
        string order,
        TContext context);

    /// <summary>Persist a newly created thread item.</summary>
    Task AddThreadItemAsync(string threadId, ThreadItem item, TContext context);

    /// <summary>Upsert a thread item by id.</summary>
    Task SaveItemAsync(string threadId, ThreadItem item, TContext context);

    /// <summary>Load a thread item by id.</summary>
    Task<ThreadItem> LoadItemAsync(string threadId, string itemId, TContext context);

    /// <summary>Delete a thread and its items.</summary>
    Task DeleteThreadAsync(string threadId, TContext context);

    /// <summary>Delete a thread item by id.</summary>
    Task DeleteThreadItemAsync(string threadId, string itemId, TContext context);
}
