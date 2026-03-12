using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NetAgents.Config;
using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace NetAgents.Agents;

public sealed record McpResolvedTarget(string FilePath, bool Shared);

public delegate McpResolvedTarget McpTargetResolver(string agentId, McpConfigSpec spec);

public static class McpWriter
{
    public static IReadOnlyList<McpDeclaration> ToMcpDeclarations(IReadOnlyList<McpConfig> configs) =>
        configs.Select(m => new McpDeclaration(
            m.Name, m.Command, m.Args, m.Url, m.Headers,
            m.Env is { Count: > 0 } ? m.Env : null)).ToList();

    public static McpTargetResolver ProjectResolver(string projectRoot) =>
        (_, spec) => new McpResolvedTarget(Path.Combine(projectRoot, spec.FilePath), spec.Shared);

    public static async Task WriteMcpConfigsAsync(
        IReadOnlyList<string> agentIds,
        IReadOnlyList<McpDeclaration> servers,
        McpTargetResolver resolveTarget,
        CancellationToken ct = default)
    {
        if (servers.Count == 0) return;

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var id in agentIds)
        {
            var agent = AgentRegistry.GetAgent(id);
            if (agent is null) continue;

            var target = resolveTarget(id, agent.Mcp);
            if (!seen.Add(target.FilePath)) continue;

            var serialized = new JsonObject();
            foreach (var server in servers)
            {
                var (name, config) = agent.SerializeServer(server);
                serialized[name] = config;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target.FilePath)!);

            if (target.Shared)
                await MergeWriteAsync(target.FilePath, agent.Mcp, serialized, ct).ConfigureAwait(false);
            else
                await FreshWriteAsync(target.FilePath, agent.Mcp, serialized, ct).ConfigureAwait(false);
        }
    }

    public static async Task<IReadOnlyList<(string Agent, string Issue)>> VerifyMcpConfigsAsync(
        IReadOnlyList<string> agentIds,
        IReadOnlyList<McpDeclaration> servers,
        McpTargetResolver resolveTarget,
        CancellationToken ct = default)
    {
        if (servers.Count == 0) return [];

        var issues = new List<(string, string)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var id in agentIds)
        {
            var agent = AgentRegistry.GetAgent(id);
            if (agent is null) continue;

            var target = resolveTarget(id, agent.Mcp);
            if (!seen.Add(target.FilePath)) continue;

            if (!File.Exists(target.FilePath))
            {
                issues.Add((id, $"MCP config missing: {target.FilePath}"));
                continue;
            }

            var expectedNames = servers.Select(s => s.Name).ToList();
            try
            {
                var existing = await ReadExistingJsonAsync(target.FilePath, agent.Mcp, ct).ConfigureAwait(false);
                var serversNode = existing[agent.Mcp.RootKey]?.AsObject();
                foreach (var name in expectedNames)
                {
                    if (serversNode is null || !serversNode.ContainsKey(name))
                        issues.Add((id, $"""MCP server "{name}" missing from {target.FilePath}"""));
                }
            }
            catch
            {
                issues.Add((id, $"Failed to read MCP config: {target.FilePath}"));
            }
        }

        return issues;
    }

    // ── Internal helpers ─────────────────────────────────────────────────────

    private static async Task FreshWriteAsync(string filePath, McpConfigSpec spec, JsonObject servers, CancellationToken ct)
    {
        var doc = new JsonObject { [spec.RootKey] = servers };
        await WriteDocAsync(filePath, doc, spec.Format, ct).ConfigureAwait(false);
    }

    private static async Task MergeWriteAsync(string filePath, McpConfigSpec spec, JsonObject servers, CancellationToken ct)
    {
        var existing = File.Exists(filePath)
            ? await ReadExistingJsonAsync(filePath, spec, ct).ConfigureAwait(false)
            : new JsonObject();

        var prev = existing[spec.RootKey]?.AsObject() ?? new JsonObject();
        // Merge: existing servers + new servers (new wins on conflict)
        foreach (var (key, value) in servers)
            prev[key] = value?.DeepClone();
        existing[spec.RootKey] = prev;

        await WriteDocAsync(filePath, existing, spec.Format, ct).ConfigureAwait(false);
    }

    private static async Task<JsonObject> ReadExistingJsonAsync(string filePath, McpConfigSpec spec, CancellationToken ct)
    {
        var raw = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        return spec.Format switch
        {
            ConfigFormat.Toml => TomlToJsonObject(raw),
            _ => JsonNode.Parse(raw)?.AsObject() ?? new JsonObject(),
        };
    }

    private static JsonObject TomlToJsonObject(string raw)
    {
        var doc = SyntaxParser.Parse(raw);
        var obj = new JsonObject();

        // Top-level key-value pairs
        for (var i = 0; i < doc.KeyValues.ChildrenCount; i++)
        {
            var kv = doc.KeyValues.GetChild(i)!;
            obj[kv.Key?.ToString().Trim() ?? ""] = kv.Value?.ToString().Trim();
        }

        // Tables
        for (var t = 0; t < doc.Tables.ChildrenCount; t++)
        {
            var table = doc.Tables.GetChild(t)!;
            var tableName = table.Name?.ToString().Trim();
            if (tableName is null) continue;
            var tableObj = new JsonObject();
            for (var i = 0; i < table.Items.ChildrenCount; i++)
            {
                var kv = table.Items.GetChild(i)!;
                tableObj[kv.Key?.ToString().Trim() ?? ""] = kv.Value?.ToString().Trim();
            }
            obj[tableName] = tableObj;
        }
        return obj;
    }

    private static async Task WriteDocAsync(string filePath, JsonObject doc, ConfigFormat format, CancellationToken ct)
    {
        var content = format switch
        {
            ConfigFormat.Toml => SerializeToml(doc),
            _ => doc.ToJsonString(JsonOptions) + "\n",
        };
        await File.WriteAllTextAsync(filePath, content, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    private static string SerializeToml(JsonObject doc)
    {
        var sb = new StringBuilder();
        foreach (var (key, value) in doc)
        {
            if (value is JsonObject section)
            {
                SerializeTomlTable(sb, key, section);
            }
            else
            {
                sb.Append(key).Append(" = ");
                AppendTomlValue(sb, value);
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    private static void SerializeTomlTable(StringBuilder sb, string tableName, JsonObject table)
    {
        foreach (var (key, value) in table)
        {
            sb.AppendLine($"[{tableName}.{key}]");
            if (value is JsonObject inner)
            {
                foreach (var (ik, iv) in inner)
                {
                    sb.Append(ik).Append(" = ");
                    AppendTomlValue(sb, iv);
                    sb.AppendLine();
                }
            }
            sb.AppendLine();
        }
    }

    private static void AppendTomlValue(StringBuilder sb, JsonNode? node)
    {
        switch (node)
        {
            case JsonArray arr:
                sb.Append('[');
                for (var i = 0; i < arr.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    AppendTomlValue(sb, arr[i]);
                }
                sb.Append(']');
                break;
            case JsonValue val when val.TryGetValue<string>(out var s):
                sb.Append('"').Append(s.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
                break;
            case JsonValue val when val.TryGetValue<int>(out var n):
                sb.Append(n);
                break;
            case JsonValue val when val.TryGetValue<long>(out var n):
                sb.Append(n);
                break;
            case JsonValue val when val.TryGetValue<bool>(out var b):
                sb.Append(b ? "true" : "false");
                break;
            default:
                sb.Append(node?.ToJsonString() ?? "null");
                break;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}
