using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Qyl.ChatKit;

/// <summary>
/// Abstract ChatKit server that routes incoming requests to streaming or non-streaming handlers.
/// Subclasses implement <see cref="RespondAsync"/> to produce thread stream events.
/// </summary>
public abstract class ChatKitServer<TContext>(
    IStore<TContext> store,
    IAttachmentStore<TContext>? attachmentStore = null,
    TimeProvider? timeProvider = null,
    ILogger? logger = null)
{
    private const int DefaultPageSize = 20;
    private const string DefaultErrorMessage = "An error occurred when generating a response.";

    protected IStore<TContext> Store { get; } = store;
    protected TimeProvider Clock { get; } = timeProvider ?? TimeProvider.System;
    protected ILogger Logger { get; } = logger ?? NullLogger.Instance;

    // -- Abstract / virtual hooks --

    /// <summary>Stream response events for a new or retried user message.</summary>
    public abstract IAsyncEnumerable<ThreadStreamEvent> RespondAsync(
        ThreadMetadata thread,
        UserMessageItem? inputUserMessage,
        TContext context,
        CancellationToken ct = default);

    /// <summary>Persist user feedback for one or more thread items.</summary>
    public virtual ValueTask AddFeedbackAsync(
        string threadId,
        IReadOnlyList<string> itemIds,
        FeedbackKind feedback,
        TContext context,
        CancellationToken ct = default) => default;

    /// <summary>Transcribe speech audio to text.</summary>
    public virtual ValueTask<TranscriptionResult> TranscribeAsync(
        AudioInput audioInput,
        TContext context,
        CancellationToken ct = default) =>
        throw new NotImplementedException(
            "TranscribeAsync() must be overridden to support the input.transcribe request.");

    /// <summary>Handle a widget or client-dispatched action and yield response events.</summary>
    public virtual IAsyncEnumerable<ThreadStreamEvent> ActionAsync(
        ThreadMetadata thread,
        Action<string, object?> action,
        WidgetItem? sender,
        TContext context,
        CancellationToken ct = default) =>
        throw new NotImplementedException(
            "ActionAsync() must be overridden to react to actions.");

    /// <summary>Handle a synchronous custom action and return a single item update.</summary>
    public virtual ValueTask<SyncCustomActionResponse> SyncActionAsync(
        ThreadMetadata thread,
        Action<string, object?> action,
        WidgetItem? sender,
        TContext context,
        CancellationToken ct = default) =>
        throw new NotImplementedException(
            "SyncActionAsync() must be overridden to react to sync actions.");

    /// <summary>Return stream-level runtime options. Allows cancellation by default.</summary>
    public virtual StreamOptions GetStreamOptions(ThreadMetadata thread, TContext context) =>
        new() { AllowCancel = true };

    /// <summary>
    /// Perform cleanup when a stream is cancelled. The default implementation persists
    /// non-empty pending assistant messages and adds a hidden context marker.
    /// </summary>
    public virtual async ValueTask HandleStreamCancelledAsync(
        ThreadMetadata thread,
        IReadOnlyList<ThreadItem> pendingItems,
        TContext context,
        CancellationToken ct = default)
    {
        foreach (var item in pendingItems)
        {
            if (item is not AssistantMessageItem assistant)
                continue;

            var isEmpty = assistant.Content.Count == 0 ||
                          assistant.Content.All(c => string.IsNullOrWhiteSpace(c.Text));
            if (!isEmpty)
                await Store.AddThreadItemAsync(thread.Id, assistant, context);
        }

        await Store.AddThreadItemAsync(
            thread.Id,
            new SdkHiddenContextItem
            {
                Id = Store.GenerateItemId(StoreItemType.SdkHiddenContext, thread, context),
                ThreadId = thread.Id,
                CreatedAt = Clock.GetUtcNow().UtcDateTime,
                Content = "The user cancelled the stream. Stop responding to the prior request.",
            },
            context);
    }

    // -- Main entry point --

    /// <summary>Parse an incoming request and route it to the appropriate handler.</summary>
    public async ValueTask<object> ProcessAsync(
        string request, TContext context, CancellationToken ct = default)
    {
        var parsed = JsonSerializer.Deserialize<ChatKitRequest>(request, ChatKitJsonOptions.Default)
                     ?? throw new JsonException("Failed to deserialize ChatKit request.");

        if (ChatKitRequest.IsStreamingRequest(parsed))
            return new StreamingResult(ProcessStreamingAsync(parsed, context, ct));

        return new NonStreamingResult(
            await ProcessNonStreamingAsync(parsed, context, ct));
    }

    // -- Non-streaming --

    private async Task<byte[]> ProcessNonStreamingAsync(
        ChatKitRequest request, TContext context, CancellationToken ct)
    {
        switch (request)
        {
            case ThreadsGetByIdReq r:
            {
                var thread = await LoadFullThreadAsync(r.Params.ThreadId, context, ct);
                return Serialize(ToThreadResponse(thread));
            }

            case ThreadsListReq r:
            {
                var p = r.Params;
                var threads = await Store.LoadThreadsAsync(
                    p.Limit ?? DefaultPageSize, p.After, p.Order, context);
                return Serialize(new Page<Thread>
                {
                    HasMore = threads.HasMore,
                    After = threads.After,
                    Data = threads.Data.Select(t => ToThreadResponse(t)).ToList(),
                });
            }

            case ItemsFeedbackReq r:
            {
                await AddFeedbackAsync(
                    r.Params.ThreadId, r.Params.ItemIds, r.Params.Kind, context, ct);
                return "{}"u8.ToArray();
            }

            case AttachmentsCreateReq r:
            {
                var attachStore = GetAttachmentStore();
                var attachment = await attachStore.CreateAttachmentAsync(r.Params, context);
                await Store.SaveAttachmentAsync(attachment, context);
                return Serialize(attachment);
            }

            case AttachmentsDeleteReq r:
            {
                var attachStore = GetAttachmentStore();
                await attachStore.DeleteAttachmentAsync(r.Params.AttachmentId, context);
                await Store.DeleteAttachmentAsync(r.Params.AttachmentId, context);
                return "{}"u8.ToArray();
            }

            case InputTranscribeReq r:
            {
                var audioBytes = Convert.FromBase64String(r.Params.AudioBase64);
                var result = await TranscribeAsync(
                    new AudioInput { Data = audioBytes, MimeType = r.Params.MimeType }, context, ct);
                return Serialize(result);
            }

            case ItemsListReq r:
            {
                var p = r.Params;
                var items = await Store.LoadThreadItemsAsync(
                    p.ThreadId, p.After, p.Limit ?? DefaultPageSize, p.Order, context);
                // Filter out hidden context items
                var filtered = items.Data
                    .Where(i => i is not (HiddenContextItem or SdkHiddenContextItem))
                    .ToList();
                return Serialize(new Page<ThreadItem>
                {
                    Data = filtered,
                    HasMore = items.HasMore,
                    After = items.After,
                });
            }

            case ThreadsUpdateReq r:
            {
                var thread = await Store.LoadThreadAsync(r.Params.ThreadId, context);
                thread = thread with { Title = r.Params.Title };
                await Store.SaveThreadAsync(thread, context);
                return Serialize(ToThreadResponse(thread));
            }

            case ThreadsDeleteReq r:
            {
                await Store.DeleteThreadAsync(r.Params.ThreadId, context);
                return "{}"u8.ToArray();
            }

            case ThreadsSyncCustomActionReq r:
                return await ProcessSyncCustomActionAsync(r, context, ct);

            default:
                throw new InvalidOperationException(
                    $"Unknown non-streaming request type: {request.GetType().Name}");
        }
    }

    private async Task<byte[]> ProcessSyncCustomActionAsync(
        ThreadsSyncCustomActionReq request, TContext context, CancellationToken ct)
    {
        var threadMeta = await Store.LoadThreadAsync(request.Params.ThreadId, context);

        WidgetItem? senderWidget = null;
        if (request.Params.ItemId is not null)
        {
            var loaded = await Store.LoadItemAsync(
                request.Params.ThreadId, request.Params.ItemId, context);
            senderWidget = loaded as WidgetItem
                           ?? throw new InvalidOperationException(
                               "threads.sync_custom_action requires a widget sender item");
        }

        var result = await SyncActionAsync(
            threadMeta, request.Params.Action, senderWidget, context, ct);
        return Serialize(result);
    }

    // -- Streaming --

    private async IAsyncEnumerable<byte[]> ProcessStreamingAsync(
        ChatKitRequest request, TContext context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        IAsyncEnumerable<ThreadStreamEvent> events;
        try
        {
            events = ProcessStreamingImplAsync(request, context, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogError(ex, "Error while generating streamed response");
            throw;
        }

        await foreach (var evt in events.WithCancellation(ct))
        {
            var json = JsonSerializer.SerializeToUtf8Bytes<ThreadStreamEvent>(evt, ChatKitJsonOptions.Default);
            var chunk = new byte[6 + json.Length + 2]; // "data: " + json + "\n\n"
            "data: "u8.CopyTo(chunk);
            json.CopyTo(chunk, 6);
            chunk[^2] = (byte)'\n';
            chunk[^1] = (byte)'\n';
            yield return chunk;
        }
    }

    private async IAsyncEnumerable<ThreadStreamEvent> ProcessStreamingImplAsync(
        ChatKitRequest request, TContext context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        switch (request)
        {
            case ThreadsCreateReq r:
            {
                var thread = new Thread
                {
                    Id = Store.GenerateThreadId(context),
                    CreatedAt = Clock.GetUtcNow().UtcDateTime,
                    Items = new Page<ThreadItem>(),
                };
                await Store.SaveThreadAsync(thread, context);
                yield return new ThreadCreatedEvent { Thread = ToThreadResponse(thread) };

                var userMessage = await BuildUserMessageItemAsync(r.Params.Input, thread, context);
                await foreach (var evt in ProcessNewThreadItemRespondAsync(thread, userMessage, context, ct))
                    yield return evt;
                break;
            }

            case ThreadsAddUserMessageReq r:
            {
                var thread = await Store.LoadThreadAsync(r.Params.ThreadId, context);
                var userMessage = await BuildUserMessageItemAsync(r.Params.Input, thread, context);
                await foreach (var evt in ProcessNewThreadItemRespondAsync(thread, userMessage, context, ct))
                    yield return evt;
                break;
            }

            case ThreadsAddClientToolOutputReq r:
            {
                var thread = await Store.LoadThreadAsync(r.Params.ThreadId, context);
                var items = await Store.LoadThreadItemsAsync(thread.Id, null, 1, "desc", context);
                var toolCall = items.Data
                    .OfType<ClientToolCallItem>()
                    .FirstOrDefault(i => i.Status == "pending")
                    ?? throw new InvalidOperationException(
                        $"Last thread item in {thread.Id} was not a ClientToolCallItem");

                var completed = toolCall with { Output = r.Params.Result, Status = "completed" };
                await Store.SaveItemAsync(thread.Id, completed, context);
                await CleanupPendingClientToolCallAsync(thread, context);

                await foreach (var evt in ProcessEventsAsync(
                    thread, context, ct => RespondAsync(thread, null, context, ct), ct))
                    yield return evt;
                break;
            }

            case ThreadsRetryAfterItemReq r:
            {
                var threadMeta = await Store.LoadThreadAsync(r.Params.ThreadId, context);
                List<ThreadItem> itemsToRemove = [];
                UserMessageItem? userMessageItem = null;

                await foreach (var item in PaginateThreadItemsReverseAsync(r.Params.ThreadId, context, ct))
                {
                    if (item.Id == r.Params.ItemId)
                    {
                        userMessageItem = item as UserMessageItem
                            ?? throw new InvalidOperationException(
                                $"Item {r.Params.ItemId} is not a user message");
                        break;
                    }
                    itemsToRemove.Add(item);
                }

                if (userMessageItem is not null)
                {
                    foreach (var item in itemsToRemove)
                        await Store.DeleteThreadItemAsync(r.Params.ThreadId, item.Id, context);

                    await foreach (var evt in ProcessEventsAsync(
                        threadMeta, context,
                        ct => RespondAsync(threadMeta, userMessageItem, context, ct), ct))
                        yield return evt;
                }
                break;
            }

            case ThreadsCustomActionReq r:
            {
                var threadMeta = await Store.LoadThreadAsync(r.Params.ThreadId, context);

                WidgetItem? senderWidget = null;
                if (r.Params.ItemId is not null)
                {
                    var loaded = await Store.LoadItemAsync(
                        r.Params.ThreadId, r.Params.ItemId, context);
                    if (loaded is not WidgetItem widget)
                    {
                        yield return new ErrorEvent
                        {
                            Code = "stream.error",
                            AllowRetry = false,
                        };
                        yield break;
                    }
                    senderWidget = widget;
                }

                await foreach (var evt in ProcessEventsAsync(
                    threadMeta, context,
                    ct => ActionAsync(threadMeta, r.Params.Action, senderWidget, context, ct), ct))
                    yield return evt;
                break;
            }

            default:
                throw new InvalidOperationException(
                    $"Unknown streaming request type: {request.GetType().Name}");
        }
    }

    // -- Core event processing loop --

    private async IAsyncEnumerable<ThreadStreamEvent> ProcessNewThreadItemRespondAsync(
        ThreadMetadata thread,
        UserMessageItem item,
        TContext context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var attachment in item.Attachments)
            await Store.SaveAttachmentAsync(attachment, context);

        await Store.AddThreadItemAsync(thread.Id, item, context);
        yield return new ThreadItemDoneEvent { Item = item };

        await foreach (var evt in ProcessEventsAsync(
            thread, context, ct => RespondAsync(thread, item, context, ct), ct))
            yield return evt;
    }

    private IAsyncEnumerable<ThreadStreamEvent> ProcessEventsAsync(
        ThreadMetadata thread,
        TContext context,
        Func<CancellationToken, IAsyncEnumerable<ThreadStreamEvent>> streamFactory,
        CancellationToken ct = default)
    {
        var channel = System.Threading.Channels.Channel.CreateUnbounded<ThreadStreamEvent>();
        _ = RunEventLoopAsync(thread, context, streamFactory, channel.Writer, ct);
        return channel.Reader.ReadAllAsync(ct);
    }

    /// <summary>
    /// Core event loop: reads from the response stream, persists state, and writes
    /// client-visible events to <paramref name="writer"/>. Error handling converts
    /// known exceptions to <see cref="ErrorEvent"/> frames on the SSE stream.
    /// </summary>
    private async Task RunEventLoopAsync(
        ThreadMetadata thread,
        TContext context,
        Func<CancellationToken, IAsyncEnumerable<ThreadStreamEvent>> streamFactory,
        System.Threading.Channels.ChannelWriter<ThreadStreamEvent> writer,
        CancellationToken ct)
    {
        var lastThread = thread;
        Dictionary<string, ThreadItem> pendingItems = [];

        try
        {
            await Task.Yield(); // allow the response to start streaming

            await writer.WriteAsync(new StreamOptionsEvent
            {
                StreamOptions = GetStreamOptions(thread, context),
            }, ct);

            await foreach (var evt in streamFactory(ct).WithCancellation(ct))
            {
                if (evt is ThreadItemAddedEvent added)
                    pendingItems[added.Item.Id] = added.Item;

                switch (evt)
                {
                    case ThreadItemDoneEvent done:
                        await Store.AddThreadItemAsync(thread.Id, done.Item, context);
                        pendingItems.Remove(done.Item.Id);
                        break;

                    case ThreadItemRemovedEvent removed:
                        await Store.DeleteThreadItemAsync(thread.Id, removed.ItemId, context);
                        pendingItems.Remove(removed.ItemId);
                        break;

                    case ThreadItemReplacedEvent replaced:
                        await Store.SaveItemAsync(thread.Id, replaced.Item, context);
                        pendingItems.Remove(replaced.Item.Id);
                        break;

                    case ThreadItemUpdatedEvent updated:
                        UpdatePendingItems(pendingItems, updated);
                        break;
                }

                // Don't send hidden context items back to the client
                var shouldSwallow = evt is ThreadItemDoneEvent { Item: HiddenContextItem or SdkHiddenContextItem };
                if (!shouldSwallow)
                    await writer.WriteAsync(evt, ct);

                if (thread != lastThread)
                {
                    lastThread = thread;
                    await Store.SaveThreadAsync(thread, context);
                    await writer.WriteAsync(
                        new ThreadUpdatedEvent { Thread = ToThreadResponse(thread) }, ct);
                }
            }

            if (thread != lastThread)
            {
                lastThread = thread;
                await Store.SaveThreadAsync(thread, context);
                await writer.WriteAsync(
                    new ThreadUpdatedEvent { Thread = ToThreadResponse(thread) }, ct);
            }
        }
        catch (OperationCanceledException)
        {
            await HandleStreamCancelledAsync(
                thread, pendingItems.Values.ToList(), context, CancellationToken.None);
            writer.Complete();
            return;
        }
        catch (CustomStreamError e)
        {
            await writer.WriteAsync(new ErrorEvent
            {
                Code = "custom",
                Message = e.Message,
                AllowRetry = e.AllowRetry,
            }, CancellationToken.None);
        }
        catch (StreamError e)
        {
            await writer.WriteAsync(new ErrorEvent
            {
                Code = e.Code.ToString(),
                AllowRetry = e.AllowRetry,
            }, CancellationToken.None);
        }
        catch (Exception e) when (e is not OutOfMemoryException and not StackOverflowException)
        {
            // Intentional: the SSE protocol requires delivering an error frame to the client
            // rather than dropping the connection. The exception is fully logged.
            Logger.LogError(e, "Unhandled exception in stream processing");
            await writer.WriteAsync(new ErrorEvent
            {
                Code = "stream.error",
                AllowRetry = true,
            }, CancellationToken.None);
        }

        // Final thread-change check after errors
        if (thread != lastThread)
        {
            await Store.SaveThreadAsync(thread, context);
            await writer.WriteAsync(
                new ThreadUpdatedEvent { Thread = ToThreadResponse(thread) }, CancellationToken.None);
        }

        writer.Complete();
    }

    // -- Pending item tracking --

    private void UpdatePendingItems(
        Dictionary<string, ThreadItem> pendingItems,
        ThreadItemUpdatedEvent evt)
    {
        if (!pendingItems.TryGetValue(evt.ItemId, out var updatedItem))
            return;

        switch (updatedItem)
        {
            case AssistantMessageItem assistant when evt.Update is
                AssistantMessageContentPartAdded or
                AssistantMessageContentPartTextDelta or
                AssistantMessageContentPartAnnotationAdded or
                AssistantMessageContentPartDone:
            {
                // Build updated content
                var content = assistant.Content.ToList();
                int targetIndex = evt.Update switch
                {
                    AssistantMessageContentPartAdded u => u.ContentIndex,
                    AssistantMessageContentPartTextDelta u => u.ContentIndex,
                    AssistantMessageContentPartAnnotationAdded u => u.ContentIndex,
                    AssistantMessageContentPartDone u => u.ContentIndex,
                    _ => -1,
                };

                while (content.Count <= targetIndex)
                    content.Add(new AssistantMessageContent { Text = "", Annotations = [] });

                switch (evt.Update)
                {
                    case AssistantMessageContentPartAdded u:
                        content[u.ContentIndex] = u.Content;
                        break;
                    case AssistantMessageContentPartTextDelta u:
                        var existing = content[u.ContentIndex];
                        content[u.ContentIndex] = existing with { Text = existing.Text + u.Delta };
                        break;
                    case AssistantMessageContentPartAnnotationAdded u:
                        var part = content[u.ContentIndex];
                        var annotations = part.Annotations.ToList();
                        if (u.AnnotationIndex <= annotations.Count)
                            annotations.Insert(u.AnnotationIndex, u.Annotation);
                        else
                            annotations.Add(u.Annotation);
                        content[u.ContentIndex] = part with { Annotations = annotations };
                        break;
                    case AssistantMessageContentPartDone u:
                        content[u.ContentIndex] = u.Content;
                        break;
                }

                pendingItems[evt.ItemId] = assistant with { Content = content };
                break;
            }

            case WorkflowItem workflow when evt.Update is WorkflowTaskUpdated or WorkflowTaskAdded:
            {
                var tasks = workflow.Workflow.Tasks.ToList();

                switch (evt.Update)
                {
                    case WorkflowTaskUpdated u:
                        tasks[u.TaskIndex] = u.Task;
                        break;
                    case WorkflowTaskAdded u:
                        tasks.Add(u.Task);
                        break;
                }

                pendingItems[evt.ItemId] = workflow with
                {
                    Workflow = workflow.Workflow with { Tasks = tasks },
                };
                break;
            }
        }
    }

    // -- Helpers --

    private async ValueTask<UserMessageItem> BuildUserMessageItemAsync(
        UserMessageInput input, ThreadMetadata thread, TContext context)
    {
        var attachments = new List<AttachmentBase>(input.Attachments.Count);
        foreach (var attachmentId in input.Attachments)
        {
            var att = await Store.LoadAttachmentAsync(attachmentId, context);
            attachments.Add(att with { ThreadId = thread.Id });
        }

        return new UserMessageItem
        {
            Id = Store.GenerateItemId(StoreItemType.Message, thread, context),
            Content = input.Content,
            ThreadId = thread.Id,
            Attachments = attachments,
            QuotedText = input.QuotedText,
            InferenceOptions = input.InferenceOptions,
            CreatedAt = Clock.GetUtcNow().UtcDateTime,
        };
    }

    private async Task<Thread> LoadFullThreadAsync(
        string threadId, TContext context, CancellationToken ct)
    {
        var meta = await Store.LoadThreadAsync(threadId, context);
        var items = await Store.LoadThreadItemsAsync(
            threadId, null, DefaultPageSize, "asc", context);

        return new Thread
        {
            Id = meta.Id,
            Title = meta.Title,
            CreatedAt = meta.CreatedAt,
            Status = meta.Status,
            AllowedImageDomains = meta.AllowedImageDomains,
            Items = items,
        };
    }

    private async IAsyncEnumerable<ThreadItem> PaginateThreadItemsReverseAsync(
        string threadId, TContext context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        string? after = null;
        while (true)
        {
            var items = await Store.LoadThreadItemsAsync(
                threadId, after, DefaultPageSize, "desc", context);

            foreach (var item in items.Data)
                yield return item;

            if (!items.HasMore)
                break;
            after = items.After;
        }
    }

    private async ValueTask CleanupPendingClientToolCallAsync(
        ThreadMetadata thread, TContext context)
    {
        var items = await Store.LoadThreadItemsAsync(
            thread.Id, null, DefaultPageSize, "desc", context);

        foreach (var item in items.Data.OfType<ClientToolCallItem>())
        {
            if (item.Status == "pending")
            {
                Logger.LogWarning("Client tool call {CallId} was not completed, ignoring", item.CallId);
                await Store.DeleteThreadItemAsync(thread.Id, item.Id, context);
            }
        }
    }

    private IAttachmentStore<TContext> GetAttachmentStore() =>
        attachmentStore ?? throw new InvalidOperationException(
            "AttachmentStore is not configured. Provide an IAttachmentStore<TContext> to handle file operations.");

    private static byte[] Serialize<T>(T obj) =>
        JsonSerializer.SerializeToUtf8Bytes(obj, ChatKitJsonOptions.Default);

    private static Thread ToThreadResponse(ThreadMetadata thread)
    {
        var items = thread is Thread full ? full.Items : new Page<ThreadItem>();
        var filtered = items.Data
            .Where(i => i is not (HiddenContextItem or SdkHiddenContextItem))
            .ToList();

        return new Thread
        {
            Id = thread.Id,
            Title = thread.Title,
            CreatedAt = thread.CreatedAt,
            Status = thread.Status,
            AllowedImageDomains = thread.AllowedImageDomains,
            Items = new Page<ThreadItem>
            {
                Data = filtered,
                HasMore = items.HasMore,
                After = items.After,
            },
        };
    }
}
