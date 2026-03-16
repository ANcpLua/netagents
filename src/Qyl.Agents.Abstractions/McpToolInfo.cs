namespace Qyl.Agents;

/// <summary>
///     Describes a single MCP tool. Returned by the generated <c>GetToolInfos()</c> method.
/// </summary>
public sealed class McpToolInfo
{
    /// <summary>Tool name as advertised in the MCP <c>tools/list</c> response.</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable description of the tool.</summary>
    public string? Description { get; init; }

    /// <summary>UTF-8 encoded JSON Schema describing the tool's input parameters.</summary>
    public byte[] InputSchema { get; init; } = Array.Empty<byte>();

    /// <summary>Safety annotation: read-only hint. Emitted as <c>readOnlyHint</c> in MCP wire format.</summary>
    public bool? ReadOnlyHint { get; init; }

    /// <summary>Safety annotation: idempotent hint. Emitted as <c>idempotentHint</c> in MCP wire format.</summary>
    public bool? IdempotentHint { get; init; }

    /// <summary>Safety annotation: destructive hint. Emitted as <c>destructiveHint</c> in MCP wire format.</summary>
    public bool? DestructiveHint { get; init; }

    /// <summary>Safety annotation: open-world hint. Emitted as <c>openWorldHint</c> in MCP wire format.</summary>
    public bool? OpenWorldHint { get; init; }
}
