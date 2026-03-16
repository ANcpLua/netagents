namespace Qyl.Agents;

using System.Text.Json;

/// <summary>
///     Implemented by generated partial classes marked with <c>[McpServer]</c>.
///     The runtime uses this interface to discover and invoke tools without reflection.
/// </summary>
public interface IMcpServer
{
    /// <summary>Returns SKILL.md content for dotagents distribution.</summary>
    static abstract string SkillMd { get; }

    /// <summary>Returns llms.txt content for LLM indexing.</summary>
    static abstract string LlmsTxt { get; }

    /// <summary>Returns server identity for the MCP <c>initialize</c> response.</summary>
    static abstract McpServerInfo GetServerInfo();

    /// <summary>Returns all tool descriptors for the MCP <c>tools/list</c> response.</summary>
    static abstract IReadOnlyList<McpToolInfo> GetToolInfos();

    /// <summary>Returns all resource descriptors for the MCP <c>resources/list</c> response.</summary>
    static abstract IReadOnlyList<McpResourceInfo> GetResourceInfos();

    /// <summary>Returns all prompt descriptors for the MCP <c>prompts/list</c> response.</summary>
    static abstract IReadOnlyList<McpPromptInfo> GetPromptInfos();

    /// <summary>Dispatches a tool call by name, deserializing arguments and serializing the result.</summary>
    Task<string> DispatchToolCallAsync(
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken = default);

    /// <summary>Dispatches a resource read by URI, returning content with binary indicator.</summary>
    Task<ResourceReadResult> DispatchResourceReadAsync(
        string uri,
        CancellationToken cancellationToken = default);

    /// <summary>Dispatches a prompt by name, deserializing arguments and returning the result.</summary>
    Task<PromptResult> DispatchPromptAsync(
        string name,
        JsonElement arguments,
        CancellationToken cancellationToken = default);
}
