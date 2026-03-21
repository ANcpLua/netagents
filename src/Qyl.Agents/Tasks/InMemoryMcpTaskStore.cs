namespace Qyl.Agents.Tasks;

using System.Collections.Concurrent;

/// <summary>
///     In-memory implementation of <see cref="IMcpTaskStore" />.
///     Suitable for single-instance deployments. Not durable across restarts.
///     Experimental: <c>QYLEXP001</c> — the Tasks feature may change as the MCP specification evolves.
/// </summary>
public sealed class InMemoryMcpTaskStore(TimeProvider timeProvider) : IMcpTaskStore
{
    private readonly ConcurrentDictionary<string, McpTask> _tasks = new(StringComparer.Ordinal);

    public InMemoryMcpTaskStore() : this(TimeProvider.System)
    {
    }

    public Task CreateAsync(McpTask task, CancellationToken ct = default)
    {
        if (!_tasks.TryAdd(task.TaskId, task))
            throw new InvalidOperationException($"Task '{task.TaskId}' already exists.");
        return Task.CompletedTask;
    }

    public Task<McpTask?> GetAsync(string taskId, CancellationToken ct = default)
    {
        _tasks.TryGetValue(taskId, out var task);
        return Task.FromResult(task);
    }

    public Task<IReadOnlyList<McpTask>> ListAsync(McpTaskStatus? statusFilter = null, CancellationToken ct = default)
    {
        IReadOnlyList<McpTask> result = statusFilter switch
        {
            { } filter => _tasks.Values.Where(t => t.Status == filter).ToArray(),
            _ => _tasks.Values.ToArray()
        };
        return Task.FromResult(result);
    }

    public Task UpdateAsync(McpTask task, CancellationToken ct = default)
    {
        _tasks[task.TaskId] = task;
        return Task.CompletedTask;
    }

    public Task<bool> CancelAsync(string taskId, CancellationToken ct = default)
    {
        if (!_tasks.TryGetValue(taskId, out var task))
            return Task.FromResult(false);

        if (task.Status is McpTaskStatus.Completed or McpTaskStatus.Failed or McpTaskStatus.Cancelled)
            return Task.FromResult(false);

        task.Status = McpTaskStatus.Cancelled;
        task.LastUpdatedAt = timeProvider.GetUtcNow();
        return Task.FromResult(true);
    }
}
