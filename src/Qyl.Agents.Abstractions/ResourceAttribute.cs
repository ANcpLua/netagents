namespace Qyl.Agents;

using System;

/// <summary>
///     Marks a method as an MCP resource within an <see cref="McpServerAttribute" /> class.
///     The source generator will produce resource dispatch code and metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ResourceAttribute : Attribute
{
    public ResourceAttribute(string uri)
    {
        Uri = uri;
    }

    /// <summary>Resource URI for MCP protocol (e.g. "config://agents.toml").</summary>
    public string Uri { get; }

    /// <summary>Display name for the resource. Defaults to the method name.</summary>
    public string? Name { get; set; }

    /// <summary>MIME type of the resource content (e.g. "application/toml").</summary>
    public string? MimeType { get; set; }

    /// <summary>Human-readable description. Defaults to XML doc summary on the method.</summary>
    public string? Description { get; set; }
}
