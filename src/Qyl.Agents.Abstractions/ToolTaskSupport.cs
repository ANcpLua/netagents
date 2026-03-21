namespace Qyl.Agents;

/// <summary>
///     Declares whether a tool supports long-running task execution.
///     Emitted as <c>execution.taskSupport</c> in the MCP <c>tools/list</c> response.
/// </summary>
public enum ToolTaskSupport : byte
{
    /// <summary>Developer did not declare — omitted from wire format.</summary>
    Unset = 0,

    /// <summary>Task execution is forbidden for this tool.</summary>
    Forbidden = 1,

    /// <summary>Task execution is optional — server may return a task or an immediate result.</summary>
    Optional = 2,

    /// <summary>Task execution is required — server always returns a task.</summary>
    Required = 3
}
