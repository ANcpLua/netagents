using System.Text.Json.Nodes;
using NetAgents.Config;

namespace NetAgents.Agents;

// ── MCP types ────────────────────────────────────────────────────────────────

/// <summary>
/// Universal MCP server declaration from agents.toml [[mcp]] sections.
/// Represents either a stdio or HTTP server.
/// </summary>
public sealed record McpDeclaration(
    string Name,
    string? Command = null,
    IReadOnlyList<string>? Args = null,
    string? Url = null,
    IReadOnlyDictionary<string, string>? Headers = null,
    IReadOnlyList<string>? Env = null);

/// <summary>
/// Describes how an agent writes its MCP config file.
/// </summary>
public sealed record McpConfigSpec(
    string FilePath,
    string RootKey,
    ConfigFormat Format,
    bool Shared);

/// <summary>
/// Describes how an agent writes its hook config file.
/// </summary>
public sealed record HookConfigSpec(
    string FilePath,
    string RootKey,
    bool Shared,
    IReadOnlyDictionary<string, JsonNode>? ExtraFields = null);

public enum ConfigFormat { Json, Toml }

// ── Hook types ───────────────────────────────────────────────────────────────

/// <summary>
/// Universal hook declaration from agents.toml [[hooks]] sections.
/// </summary>
public sealed record HookDeclaration(HookEvent Event, string? Matcher, string Command);

// ── Serializer delegates ─────────────────────────────────────────────────────

/// <summary>
/// Transforms a universal McpDeclaration into the agent-specific shape
/// for its config file. Returns (serverName, serverConfig).
/// </summary>
public delegate (string Name, JsonNode Config) McpSerializer(McpDeclaration server);

/// <summary>
/// Transforms universal HookDeclarations into the agent-specific shape.
/// Returns the full value for the rootKey.
/// </summary>
public delegate JsonNode HookSerializer(IReadOnlyList<HookDeclaration> hooks);

// ── Agent definition ─────────────────────────────────────────────────────────

/// <summary>
/// Definition of an agent tool that netagents manages.
/// </summary>
public sealed record AgentDefinition(
    string Id,
    string DisplayName,
    string ConfigDir,
    string? SkillsParentDir,
    IReadOnlyList<string>? UserSkillsParentDirs,
    McpConfigSpec Mcp,
    McpSerializer SerializeServer,
    HookConfigSpec? Hooks,
    HookSerializer? SerializeHooks);

// ── Exception ────────────────────────────────────────────────────────────────

public sealed class UnsupportedFeatureException(string agentId, string feature)
    : Exception($"""Agent "{agentId}" does not support {feature}""");
