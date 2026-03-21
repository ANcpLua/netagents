namespace Qyl.Agents.Tasks;

/// <summary>
///     Represents a long-running MCP task created by a tool invocation.
///     Experimental: <c>QYLEXP001</c> — the Tasks feature may change as the MCP specification evolves.
/// </summary>
public sealed class McpTask
{
    /// <summary>Unique identifier for this task.</summary>
    public required string TaskId { get; init; }

    /// <summary>Current status of the task.</summary>
    public McpTaskStatus Status { get; set; }

    /// <summary>Optional human-readable status message.</summary>
    public string? StatusMessage { get; set; }

    /// <summary>When the task was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the task was last updated.</summary>
    public DateTimeOffset LastUpdatedAt { get; set; }

    /// <summary>Time-to-live after which the task may be garbage collected.</summary>
    public TimeSpan? TimeToLive { get; set; }

    /// <summary>Suggested poll interval for clients.</summary>
    public TimeSpan? PollInterval { get; set; }

    /// <summary>The tool result payload, available when <see cref="Status" /> is <see cref="McpTaskStatus.Completed" />.</summary>
    public string? ResultPayload { get; set; }
}
