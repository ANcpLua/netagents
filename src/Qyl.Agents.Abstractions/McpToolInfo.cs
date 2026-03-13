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
}
