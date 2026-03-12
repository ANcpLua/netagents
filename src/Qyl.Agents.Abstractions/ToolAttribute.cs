namespace Qyl.Agents;

/// <summary>
///     Marks a method as an AI-callable tool within an <see cref="McpServerAttribute" /> class.
///     The source generator will produce dispatch code, JSON Schema, and OTel spans.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ToolAttribute : Attribute
{
    public ToolAttribute()
    {
    }

    public ToolAttribute(string name)
    {
        Name = name;
    }

    /// <summary>Tool name for MCP protocol. Defaults to method name, kebab-cased.</summary>
    public string? Name { get; set; }

    /// <summary>Tool description. Defaults to XML doc summary on the method.</summary>
    public string? Description { get; set; }
}