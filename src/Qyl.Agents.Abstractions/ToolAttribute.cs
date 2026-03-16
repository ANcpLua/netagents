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

    /// <summary>Hint that the tool performs read-only operations.</summary>
    public ToolHint ReadOnly { get; set; }

    /// <summary>Hint that the tool is idempotent (safe to retry).</summary>
    public ToolHint Idempotent { get; set; }

    /// <summary>Hint that the tool performs destructive operations.</summary>
    public ToolHint Destructive { get; set; }

    /// <summary>Hint that the tool interacts with open-world systems.</summary>
    public ToolHint OpenWorld { get; set; }
}
