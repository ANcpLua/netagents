namespace NetAgents.Agents;

using System.Text.Json.Nodes;
using Config;

public static class AgentRegistry
{
    // ── Cursor hook event map ────────────────────────────────────────────────

    private static readonly Dictionary<HookEvent, string[]> CursorEventMap = new()
    {
        [HookEvent.PreToolUse] = ["beforeShellExecution", "beforeMCPExecution"],
        [HookEvent.PostToolUse] = ["afterFileEdit"],
        [HookEvent.UserPromptSubmit] = ["beforeSubmitPrompt"],
        [HookEvent.Stop] = ["stop"]
    };

    // ── Agent definitions ────────────────────────────────────────────────────

    private static readonly AgentDefinition Claude = new(
        "claude",
        "Claude Code",
        ".claude",
        ".claude",
        [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude")],
        new McpConfigSpec(".mcp.json", "mcpServers", ConfigFormat.Json, false),
        s =>
        {
            if (s.Url is not null) return HttpServer(s, "http");
            var env = EnvRecord(s.Env, k => $"${{{k}}}");
            var obj = new JsonObject { ["command"] = s.Command, ["args"] = ToJsonArray(s.Args ?? []) };
            if (env is not null) obj["env"] = ToJsonObject(env);
            return (s.Name, obj);
        },
        new HookConfigSpec(".claude/settings.json", "hooks", true),
        SerializeClaudeHooks);

    private static readonly AgentDefinition Cursor = new(
        "cursor",
        "Cursor",
        ".cursor",
        ".claude",
        [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude")],
        new McpConfigSpec(".cursor/mcp.json", "mcpServers", ConfigFormat.Json, false),
        s => s.Url is not null ? HttpServer(s) : Claude.SerializeServer(s),
        new HookConfigSpec(".cursor/hooks.json", "hooks", false,
            new Dictionary<string, JsonNode> { ["version"] = JsonValue.Create(1) }),
        hooks =>
        {
            var result = new JsonObject();
            foreach (var h in hooks)
            {
                var cursorEvents = CursorEventMap[h.Event];
                foreach (var ce in cursorEvents)
                {
                    if (result[ce] is not JsonArray list)
                    {
                        list = [];
                        result[ce] = list;
                    }

                    list.Add((JsonNode)new JsonObject { ["command"] = h.Command });
                }
            }

            return result;
        });

    private static readonly AgentDefinition Codex = new(
        "codex",
        "Codex",
        ".codex",
        null,
        null,
        new McpConfigSpec(".codex/config.toml", "mcp_servers", ConfigFormat.Toml, true),
        s =>
        {
            if (s.Url is not null)
            {
                var obj = new JsonObject { ["url"] = s.Url };
                if (s.Headers is { Count: > 0 })
                    obj["http_headers"] = ToJsonObject(s.Headers);
                return (s.Name, obj);
            }

            var env = EnvRecord(s.Env, k => $"${{{k}}}");
            var result = new JsonObject { ["command"] = s.Command, ["args"] = ToJsonArray(s.Args ?? []) };
            if (env is not null) result["env"] = ToJsonObject(env);
            return (s.Name, result);
        },
        null,
        _ => throw new UnsupportedFeatureException("codex", "hooks"));

    private static readonly AgentDefinition VsCode = new(
        "vscode",
        "VS Code Copilot",
        ".vscode",
        null,
        null,
        new McpConfigSpec(".vscode/mcp.json", "servers", ConfigFormat.Json, false),
        s =>
        {
            if (s.Url is not null) return HttpServer(s, "http");
            var env = EnvRecord(s.Env, k => $"${{input:{k}}}");
            var obj = new JsonObject
                { ["type"] = "stdio", ["command"] = s.Command, ["args"] = ToJsonArray(s.Args ?? []) };
            if (env is not null) obj["env"] = ToJsonObject(env);
            return (s.Name, obj);
        },
        new HookConfigSpec(".claude/settings.json", "hooks", true),
        SerializeClaudeHooks);

    private static readonly AgentDefinition OpenCode = new(
        "opencode",
        "OpenCode",
        ".claude",
        null,
        null,
        new McpConfigSpec("opencode.json", "mcp", ConfigFormat.Json, true),
        s =>
        {
            if (s.Url is not null) return HttpServer(s, "remote");
            var env = EnvRecord(s.Env, k => $"${{{k}}}");
            var cmdList = new JsonArray(JsonValue.Create(s.Command!));
            foreach (var arg in s.Args ?? [])
                cmdList.Add((JsonNode?)JsonValue.Create(arg));
            var obj = new JsonObject { ["type"] = "local", ["command"] = cmdList };
            if (env is not null) obj["environment"] = ToJsonObject(env);
            return (s.Name, obj);
        },
        null,
        _ => throw new UnsupportedFeatureException("opencode", "hooks"));

    // ── Registry ─────────────────────────────────────────────────────────────

    private static readonly AgentDefinition[] AllAgentsList = [Claude, Cursor, Codex, VsCode, OpenCode];

    private static readonly Dictionary<string, AgentDefinition> Registry =
        AllAgentsList.ToDictionary(a => a.Id);
    // ── Serializer helpers ───────────────────────────────────────────────────

    private static Dictionary<string, string>? EnvRecord(IReadOnlyList<string>? env, Func<string, string> template)
    {
        if (env is null or { Count: 0 }) return null;
        var rec = new Dictionary<string, string>(env.Count);
        foreach (var key in env)
            rec[key] = template(key);
        return rec;
    }

    private static (string Name, JsonNode Config) HttpServer(McpDeclaration s, string? type = null)
    {
        var obj = new JsonObject();
        if (type is not null) obj["type"] = type;
        obj["url"] = s.Url;
        if (s.Headers is { Count: > 0 })
            obj["headers"] = ToJsonObject(s.Headers);
        return (s.Name, obj);
    }

    private static JsonNode SerializeClaudeHooks(IReadOnlyList<HookDeclaration> hooks)
    {
        var result = new JsonObject();
        foreach (var h in hooks)
        {
            var entry = new JsonObject();
            if (h.Matcher is not null) entry["matcher"] = h.Matcher;
            entry["hooks"] = new JsonArray(new JsonObject { ["type"] = "command", ["command"] = h.Command });
            var key = h.Event.ToString();
            if (result[key] is not JsonArray arr)
            {
                arr = [];
                result[key] = arr;
            }

            arr.Add((JsonNode)entry);
        }

        return result;
    }

    public static AgentDefinition? GetAgent(string id)
    {
        return Registry.GetValueOrDefault(id);
    }

    public static IReadOnlyList<string> AllAgentIds()
    {
        return AllAgentsList.Select(a => a.Id).ToList();
    }

    public static IReadOnlyList<AgentDefinition> AllAgents()
    {
        return AllAgentsList;
    }

    // ── JSON helpers ─────────────────────────────────────────────────────────

    internal static JsonObject ToJsonObject(IEnumerable<KeyValuePair<string, string>> dict)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in dict)
            obj[key] = value;
        return obj;
    }

    private static JsonArray ToJsonArray(IEnumerable<string> items)
    {
        var arr = new JsonArray();
        foreach (var item in items)
            arr.Add((JsonNode?)JsonValue.Create(item));
        return arr;
    }
}
