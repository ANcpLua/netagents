using System.Threading.Channels;
using Qyl.ChatKit.Widgets;

namespace Qyl.ChatKit;

/// <summary>
/// Context object passed to agent callbacks, providing access to the store,
/// thread metadata, and a channel for emitting events back to the stream processor.
/// </summary>
public sealed class AgentContext<TContext>
{
    public required ThreadMetadata Thread { get; init; }
    public required IStore<TContext> Store { get; init; }
    public required TContext RequestContext { get; init; }
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    public string? PreviousResponseId { get; set; }
    public ClientToolCall? ClientToolCall { get; set; }
    public WorkflowItem? WorkflowItem { get; set; }
    public GeneratedImageItem? GeneratedImageItem { get; set; }

    private readonly Channel<ThreadStreamEvent> _events = Channel.CreateUnbounded<ThreadStreamEvent>();

    /// <summary>Reader for the event channel, consumed by the stream processor.</summary>
    internal ChannelReader<ThreadStreamEvent> EventReader => _events.Reader;

    /// <summary>Signal that no more events will be written.</summary>
    internal void Complete() => _events.Writer.TryComplete();

    /// <summary>Generate a new store-backed id for the given item type.</summary>
    public string GenerateId(StoreItemType type, ThreadMetadata? thread = null) =>
        type == StoreItemType.Thread
            ? Store.GenerateThreadId(RequestContext)
            : Store.GenerateItemId(type, thread ?? Thread, RequestContext);

    /// <summary>Stream a widget into the thread by enqueueing widget events.</summary>
    public async ValueTask StreamWidgetAsync(
        WidgetRoot widget, string? copyText = null, CancellationToken ct = default)
    {
        await foreach (var evt in WidgetDiff.StreamWidgetAsync(
            Thread, widget, copyText,
            t => Store.GenerateItemId(t, Thread, RequestContext),
            TimeProvider, ct))
        {
            await _events.Writer.WriteAsync(evt, ct);
        }
    }

    /// <summary>Stream an async sequence of widget roots into the thread.</summary>
    public async ValueTask StreamWidgetAsync(
        IAsyncEnumerable<WidgetRoot> widgetStream, string? copyText = null,
        CancellationToken ct = default)
    {
        await foreach (var evt in WidgetDiff.StreamWidgetAsync(
            Thread, widgetStream, copyText,
            t => Store.GenerateItemId(t, Thread, RequestContext),
            TimeProvider, ct))
        {
            await _events.Writer.WriteAsync(evt, ct);
        }
    }

    /// <summary>Begin streaming a new workflow item.</summary>
    public async ValueTask StartWorkflowAsync(Workflow workflow, CancellationToken ct = default)
    {
        WorkflowItem = new WorkflowItem
        {
            Id = GenerateId(StoreItemType.Workflow),
            CreatedAt = TimeProvider.GetUtcNow().UtcDateTime,
            Workflow = workflow,
            ThreadId = Thread.Id,
        };

        if (workflow.Type != "reasoning" && workflow.Tasks.Count == 0)
            return; // Defer sending added event until we have tasks

        await StreamAsync(new ThreadItemAddedEvent { Item = WorkflowItem }, ct);
    }

    /// <summary>Append a workflow task and stream the appropriate event.</summary>
    public async ValueTask AddWorkflowTaskAsync(ChatKitTask task, CancellationToken ct = default)
    {
        WorkflowItem ??= new WorkflowItem
        {
            Id = GenerateId(StoreItemType.Workflow),
            CreatedAt = TimeProvider.GetUtcNow().UtcDateTime,
            Workflow = new Workflow { Type = "custom", Tasks = [] },
            ThreadId = Thread.Id,
        };

        var tasks = WorkflowItem.Workflow.Tasks.ToList();
        tasks.Add(task);
        WorkflowItem = WorkflowItem with
        {
            Workflow = WorkflowItem.Workflow with { Tasks = tasks },
        };

        if (WorkflowItem.Workflow.Type != "reasoning" && tasks.Count == 1)
        {
            await StreamAsync(new ThreadItemAddedEvent { Item = WorkflowItem }, ct);
        }
        else
        {
            await StreamAsync(new ThreadItemUpdatedEvent
            {
                ItemId = WorkflowItem.Id,
                Update = new WorkflowTaskAdded
                {
                    Task = task,
                    TaskIndex = tasks.Count - 1,
                },
            }, ct);
        }
    }

    /// <summary>Update an existing workflow task and stream the delta.</summary>
    public async ValueTask UpdateWorkflowTaskAsync(
        ChatKitTask task, int taskIndex, CancellationToken ct = default)
    {
        if (WorkflowItem is null)
            throw new InvalidOperationException("Workflow is not set");

        var tasks = WorkflowItem.Workflow.Tasks.ToList();
        tasks[taskIndex] = task;
        WorkflowItem = WorkflowItem with
        {
            Workflow = WorkflowItem.Workflow with { Tasks = tasks },
        };

        await StreamAsync(new ThreadItemUpdatedEvent
        {
            ItemId = WorkflowItem.Id,
            Update = new WorkflowTaskUpdated
            {
                Task = task,
                TaskIndex = taskIndex,
            },
        }, ct);
    }

    /// <summary>Finalize the active workflow item, optionally attaching a summary.</summary>
    public async ValueTask EndWorkflowAsync(
        WorkflowSummary? summary = null, bool expanded = false, CancellationToken ct = default)
    {
        if (WorkflowItem is null)
            return;

        var finalSummary = summary ?? WorkflowItem.Workflow.Summary;
        if (finalSummary is null)
        {
            var delta = TimeProvider.GetUtcNow().UtcDateTime - WorkflowItem.CreatedAt;
            finalSummary = new DurationSummary { Duration = (int)delta.TotalSeconds };
        }

        WorkflowItem = WorkflowItem with
        {
            Workflow = WorkflowItem.Workflow with
            {
                Summary = finalSummary,
                Expanded = expanded,
            },
        };

        await StreamAsync(new ThreadItemDoneEvent { Item = WorkflowItem }, ct);
        WorkflowItem = null;
    }

    /// <summary>Enqueue a ThreadStreamEvent for downstream processing.</summary>
    public async ValueTask StreamAsync(ThreadStreamEvent evt, CancellationToken ct = default) =>
        await _events.Writer.WriteAsync(evt, ct);
}

/// <summary>Returned from tool methods to indicate a client-side tool call.</summary>
public sealed record ClientToolCall
{
    public required string Name { get; init; }
    public required Dictionary<string, object?> Arguments { get; init; }
}
