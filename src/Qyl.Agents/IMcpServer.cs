using System.Text.Json;

namespace Qyl.Agents;

/// <summary>
///     Implemented by generated partial classes marked with <c>[McpServer]</c>.
///     The runtime uses this interface to discover and invoke tools without reflection.
/// </summary>
public interface IMcpServer
{
    /// <summary>Returns SKILL.md content for dotagents distribution.</summary>
    static abstract string SkillMd { get; }

    /// <summary>Returns server identity for the MCP <c>initialize</c> response.</summary>
    static abstract McpServerInfo GetServerInfo();

    /// <summary>Returns all tool descriptors for the MCP <c>tools/list</c> response.</summary>
    static abstract IReadOnlyList<McpToolInfo> GetToolInfos();

    /// <summary>Dispatches a tool call by name, deserializing arguments and serializing the result.</summary>
    Task<string> DispatchToolCallAsync(
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken = default);
}