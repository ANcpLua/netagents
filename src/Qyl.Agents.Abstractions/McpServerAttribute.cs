namespace Qyl.Agents;

/// <summary>
///     Marks a partial class as an MCP server. The source generator will produce
///     tool dispatch, JSON Schema, OTel instrumentation, and JSON serialization context.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class McpServerAttribute : Attribute
{
    public McpServerAttribute()
    {
    }

    public McpServerAttribute(string name)
    {
        Name = name;
    }

    /// <summary>Server name for MCP protocol. Defaults to class name, kebab-cased.</summary>
    public string? Name { get; set; }

    /// <summary>Server description. Defaults to XML doc summary on the class.</summary>
    public string? Description { get; set; }

    /// <summary>Server version. Defaults to assembly informational version.</summary>
    public string? Version { get; set; }
}
