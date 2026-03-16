namespace Qyl.Agents;

using System;

/// <summary>
///     Marks a method as an MCP prompt template within an <see cref="McpServerAttribute" /> class.
///     The source generator will produce prompt dispatch code and metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class PromptAttribute : Attribute
{
    public PromptAttribute(string name)
    {
        Name = name;
    }

    /// <summary>Prompt name for MCP protocol.</summary>
    public string Name { get; }

    /// <summary>Human-readable description. Defaults to XML doc summary on the method.</summary>
    public string? Description { get; set; }
}
