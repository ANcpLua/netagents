namespace Qyl.Agents;

/// <summary>Describes a single argument for an MCP prompt template.</summary>
public sealed class McpPromptArgument
{
    /// <summary>Argument name.</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable description of the argument.</summary>
    public string? Description { get; init; }

    /// <summary>Whether this argument is required.</summary>
    public bool Required { get; init; }
}
