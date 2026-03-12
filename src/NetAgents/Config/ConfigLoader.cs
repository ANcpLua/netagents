using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace NetAgents.Config;

public static class ConfigLoader
{
    // Known agent IDs — mirrors dotagents agents/registry.ts
    private static readonly string[] ValidAgentIds = ["claude", "cursor", "codex", "vscode", "opencode"];

    public static async Task<AgentsConfig> LoadAsync(string filePath, CancellationToken ct = default)
    {
        string raw;
        try
        {
            raw = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw new ConfigException($"Config file not found: {filePath}");
        }

        var doc = SyntaxParser.Parse(raw);
        if (doc.HasErrors)
            throw new ConfigException($"Invalid TOML in {filePath}: {doc.Diagnostics}");

        var config = ParseDocument(doc, filePath);
        AgentsConfigValidator.Validate(config);
        ValidateAgentIds(config, filePath);
        return config;
    }

    private static AgentsConfig ParseDocument(DocumentSyntax doc, string filePath)
    {
        int? version = null;
        var agents = new List<string>();
        var defaultRepoSource = RepositorySource.Github;

        // Top-level key-value pairs
        for (var i = 0; i < doc.KeyValues.ChildrenCount; i++)
        {
            if (doc.KeyValues.GetChild(i) is not { } kv) continue;
            var key = GetKeyName(kv.Key);
            switch (key)
            {
                case "version" when kv.Value is IntegerValueSyntax iv:
                    version = (int)iv.Value;
                    break;
                case "agents" when kv.Value is ArraySyntax arr:
                    agents = ParseStringArray(arr);
                    break;
                case "defaultRepositorySource" when kv.Value is StringValueSyntax sv:
                    defaultRepoSource = sv.Value switch
                    {
                        "gitlab" => RepositorySource.Gitlab,
                        _ => RepositorySource.Github,
                    };
                    break;
            }
        }

        if (version is null)
            throw new ConfigException($"Invalid config in {filePath}:\n  - version: Required");

        if (version != 1)
            throw new ConfigException($"Invalid config in {filePath}:\n  - version: Expected 1, got {version}");

        ProjectConfig? project = null;
        SymlinksConfig? symlinks = null;
        TrustConfig? trust = null;
        var skills = new List<SkillDependency>();
        var mcp = new List<McpConfig>();
        var hooks = new List<HookConfig>();

        // Tables and array tables
        for (var i = 0; i < doc.Tables.ChildrenCount; i++)
        {
            var child = doc.Tables.GetChild(i);
            switch (child)
            {
                case TableSyntax table:
                    var tableName = BareKeyToString(table.Name?.Key);
                    switch (tableName)
                    {
                        case "project":
                            project = ParseProjectTable(table);
                            break;
                        case "symlinks":
                            symlinks = ParseSymlinksTable(table);
                            break;
                        case "trust":
                            trust = ParseTrustTable(table);
                            break;
                    }
                    break;

                case TableArraySyntax arrayTable:
                    var arrayName = BareKeyToString(arrayTable.Name?.Key);
                    switch (arrayName)
                    {
                        case "skills":
                            skills.Add(ParseSkillEntry(arrayTable));
                            break;
                        case "mcp":
                            mcp.Add(ParseMcpEntry(arrayTable));
                            break;
                        case "hooks":
                            hooks.Add(ParseHookEntry(arrayTable));
                            break;
                    }
                    break;
            }
        }

        return new AgentsConfig(
            Version: version.Value,
            DefaultRepositorySource: defaultRepoSource,
            Project: project,
            Symlinks: symlinks,
            Agents: agents,
            Skills: skills,
            Mcp: mcp,
            Hooks: hooks,
            Trust: trust);
    }

    private static SkillDependency ParseSkillEntry(TableArraySyntax table)
    {
        var fields = ExtractStringFields(table);
        var name = fields.GetValueOrDefault("name") ?? "";
        var source = fields.GetValueOrDefault("source") ?? "";
        var @ref = fields.GetValueOrDefault("ref");
        var path = fields.GetValueOrDefault("path");

        if (name == "*")
        {
            var exclude = ExtractStringArrayField(table, "exclude");
            return new WildcardSkillDependency(source, @ref, exclude);
        }

        return new RegularSkillDependency(name, source, @ref, path);
    }

    private static McpConfig ParseMcpEntry(TableArraySyntax table)
    {
        var fields = ExtractStringFields(table);
        var name = fields.GetValueOrDefault("name") ?? "";
        var command = fields.GetValueOrDefault("command");
        var url = fields.GetValueOrDefault("url");
        var args = ExtractStringArrayField(table, "args");
        var env = ExtractStringArrayField(table, "env");
        var headers = ExtractStringDictField(table, "headers");

        return new McpConfig(
            Name: name,
            Command: command,
            Args: args.Count > 0 ? args : null,
            Url: url,
            Headers: headers.Count > 0 ? headers : null,
            Env: env);
    }

    private static HookConfig ParseHookEntry(TableArraySyntax table)
    {
        var fields = ExtractStringFields(table);
        var eventStr = fields.GetValueOrDefault("event") ?? "";
        var matcher = fields.GetValueOrDefault("matcher");
        var command = fields.GetValueOrDefault("command") ?? "";

        var hookEvent = eventStr switch
        {
            "PreToolUse" => HookEvent.PreToolUse,
            "PostToolUse" => HookEvent.PostToolUse,
            "UserPromptSubmit" => HookEvent.UserPromptSubmit,
            "Stop" => HookEvent.Stop,
            _ => throw new ConfigException($"Unknown hook event: '{eventStr}'"),
        };

        return new HookConfig(hookEvent, matcher, command);
    }

    private static ProjectConfig ParseProjectTable(TableSyntax table)
    {
        var fields = ExtractStringFields(table);
        return new ProjectConfig(fields.GetValueOrDefault("name"));
    }

    private static SymlinksConfig ParseSymlinksTable(TableSyntax table)
    {
        var targets = ExtractStringArrayField(table, "targets");
        return new SymlinksConfig(targets);
    }

    private static TrustConfig ParseTrustTable(TableSyntax table)
    {
        var allowAll = false;
        for (var i = 0; i < table.Items.ChildrenCount; i++)
        {
            if (table.Items.GetChild(i) is not { } kv) continue;
            if (GetKeyName(kv.Key) == "allow_all" && kv.Value is BooleanValueSyntax bv)
                allowAll = bv.Value;
        }

        return new TrustConfig(
            AllowAll: allowAll,
            GithubOrgs: ExtractStringArrayField(table, "github_orgs"),
            GithubRepos: ExtractStringArrayField(table, "github_repos"),
            GitDomains: ExtractStringArrayField(table, "git_domains"));
    }

    // ── Validation helpers ───────────────────────────────────────────────────────

    private static void ValidateAgentIds(AgentsConfig config, string filePath)
    {
        var unknown = config.Agents.Where(id => !ValidAgentIds.Contains(id)).ToList();
        if (unknown.Count > 0)
            throw new ConfigException(
                $"Unknown agent(s) in {filePath}: {string.Join(", ", unknown)}. Valid agents: {string.Join(", ", ValidAgentIds)}");
    }

    // ── Syntax tree helpers ──────────────────────────────────────────────────────

    private static Dictionary<string, string> ExtractStringFields(TableArraySyntax table)
    {
        var fields = new Dictionary<string, string>();
        for (var i = 0; i < table.Items.ChildrenCount; i++)
        {
            if (table.Items.GetChild(i) is not { } kv) continue;
            if (kv.Value is StringValueSyntax { Value: not null } sv)
                fields[GetKeyName(kv.Key)] = sv.Value;
        }
        return fields;
    }

    private static Dictionary<string, string> ExtractStringFields(TableSyntax table)
    {
        var fields = new Dictionary<string, string>();
        for (var i = 0; i < table.Items.ChildrenCount; i++)
        {
            if (table.Items.GetChild(i) is not { } kv) continue;
            if (kv.Value is StringValueSyntax { Value: not null } sv)
                fields[GetKeyName(kv.Key)] = sv.Value;
        }
        return fields;
    }

    private static List<string> ExtractStringArrayField(TableArraySyntax table, string fieldName)
    {
        for (var i = 0; i < table.Items.ChildrenCount; i++)
        {
            if (table.Items.GetChild(i) is not { } kv) continue;
            if (GetKeyName(kv.Key) != fieldName) continue;
            if (kv.Value is ArraySyntax arr)
                return ParseStringArray(arr);
        }
        return [];
    }

    private static List<string> ExtractStringArrayField(TableSyntax table, string fieldName)
    {
        for (var i = 0; i < table.Items.ChildrenCount; i++)
        {
            if (table.Items.GetChild(i) is not { } kv) continue;
            if (GetKeyName(kv.Key) != fieldName) continue;
            if (kv.Value is ArraySyntax arr)
                return ParseStringArray(arr);
        }
        return [];
    }

    private static Dictionary<string, string> ExtractStringDictField(TableArraySyntax table, string fieldName)
    {
        for (var i = 0; i < table.Items.ChildrenCount; i++)
        {
            if (table.Items.GetChild(i) is not { } kv) continue;
            if (GetKeyName(kv.Key) != fieldName) continue;
            if (kv.Value is InlineTableSyntax inlineTable)
                return ParseInlineTable(inlineTable);
        }
        return new Dictionary<string, string>();
    }

    private static Dictionary<string, string> ParseInlineTable(InlineTableSyntax inlineTable)
    {
        var result = new Dictionary<string, string>();
        for (var i = 0; i < inlineTable.Items.ChildrenCount; i++)
        {
            if (inlineTable.Items.GetChild(i) is not { } item) continue;
            if (item.KeyValue is not { } kv) continue;
            if (kv.Value is StringValueSyntax { Value: not null } sv)
                result[GetKeyName(kv.Key)] = sv.Value;
        }
        return result;
    }

    private static List<string> ParseStringArray(ArraySyntax arr)
    {
        var result = new List<string>();
        for (var i = 0; i < arr.Items.ChildrenCount; i++)
        {
            if (arr.Items.GetChild(i) is not { } item) continue;
            if (item.Value is StringValueSyntax { Value: not null } sv)
                result.Add(sv.Value);
        }
        return result;
    }

    private static string GetKeyName(KeySyntax? key) => BareKeyToString(key?.Key);

    private static string BareKeyToString(BareKeyOrStringValueSyntax? key) =>
        key?.ToString()?.Trim() ?? "";
}
