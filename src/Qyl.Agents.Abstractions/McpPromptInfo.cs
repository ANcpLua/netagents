namespace Qyl.Agents;

using System;
using System.Collections.Generic;

/// <summary>
///     Describes a single MCP prompt template. Returned by the generated <c>GetPromptInfos()</c> method.
/// </summary>
public sealed class McpPromptInfo
{
    /// <summary>Prompt name as advertised in the MCP <c>prompts/list</c> response.</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable description of the prompt.</summary>
    public string? Description { get; init; }

    /// <summary>Flat argument descriptors for this prompt.</summary>
    public IReadOnlyList<McpPromptArgument> Arguments { get; init; } = Array.Empty<McpPromptArgument>();
}
