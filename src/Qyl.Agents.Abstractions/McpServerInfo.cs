namespace Qyl.Agents;

/// <summary>
///     Describes an MCP server's identity. Returned by the generated <c>GetServerInfo()</c> method.
/// </summary>
public sealed class McpServerInfo
{
    /// <summary>Server name as advertised in the MCP <c>initialize</c> response.</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable description of the server.</summary>
    public string? Description { get; init; }

    /// <summary>Semantic version string, or <c>null</c> if unversioned.</summary>
    public string? Version { get; init; }
}