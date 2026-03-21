namespace Qyl.Agents.Tasks;

/// <summary>
///     Storage backend for MCP tasks. Inject to enable task-based tool execution.
///     Experimental: <c>QYLEXP001</c> — the Tasks feature may change as the MCP specification evolves.
/// </summary>
public interface IMcpTaskStore
{
    /// <summary>Persist a newly created task.</summary>
    Task CreateAsync(McpTask task, CancellationToken ct = default);

    /// <summary>Retrieve a task by ID, or <c>null</c> if not found.</summary>
    Task<McpTask?> GetAsync(string taskId, CancellationToken ct = default);

    /// <summary>List all tasks, optionally filtered by status.</summary>
    Task<IReadOnlyList<McpTask>> ListAsync(McpTaskStatus? statusFilter = null, CancellationToken ct = default);

    /// <summary>Update an existing task's state.</summary>
    Task UpdateAsync(McpTask task, CancellationToken ct = default);

    /// <summary>Cancel a task. Returns <c>false</c> if the task was already in a terminal state.</summary>
    Task<bool> CancelAsync(string taskId, CancellationToken ct = default);
}
