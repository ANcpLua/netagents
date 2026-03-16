namespace Qyl.Agents;

/// <summary>
///     Describes a single MCP resource. Returned by the generated <c>GetResourceInfos()</c> method.
/// </summary>
public sealed class McpResourceInfo
{
    /// <summary>Resource URI as advertised in the MCP <c>resources/list</c> response.</summary>
    public required string Uri { get; init; }

    /// <summary>Human-readable name for the resource.</summary>
    public string? Name { get; init; }

    /// <summary>MIME type of the resource content.</summary>
    public string? MimeType { get; init; }

    /// <summary>Human-readable description of the resource.</summary>
    public string? Description { get; init; }
}
