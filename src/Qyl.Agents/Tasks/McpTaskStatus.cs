namespace Qyl.Agents.Tasks;

/// <summary>
///     Status of an MCP task. Terminal states: <see cref="Completed" />, <see cref="Failed" />,
///     <see cref="Cancelled" />.
///     Experimental: <c>QYLEXP001</c> — the Tasks feature may change as the MCP specification evolves.
/// </summary>
public enum McpTaskStatus : byte
{
    /// <summary>The request is currently being processed.</summary>
    Working = 0,

    /// <summary>The receiver needs input from the requestor to continue.</summary>
    InputRequired = 1,

    /// <summary>The request completed successfully (terminal).</summary>
    Completed = 2,

    /// <summary>The request did not complete successfully (terminal).</summary>
    Failed = 3,

    /// <summary>The request was cancelled before completion (terminal).</summary>
    Cancelled = 4
}
