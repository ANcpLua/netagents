using System.Text.Json;
using System.Text.Json.Nodes;
using NetAgents.Config;

namespace NetAgents.Agents;

public sealed record HookResolvedTarget(string FilePath, bool Shared);

public delegate HookResolvedTarget HookTargetResolver(string agentId, HookConfigSpec spec);

public sealed record HookWriteWarning(string Agent, string Message);

public static class HookWriter
{
    public static IReadOnlyList<HookDeclaration> ToHookDeclarations(IReadOnlyList<HookConfig> configs) =>
        configs.Select(h => new HookDeclaration(h.Event, h.Matcher, h.Command)).ToList();

    public static HookTargetResolver ProjectResolver(string projectRoot) =>
        (_, spec) => new HookResolvedTarget(Path.Combine(projectRoot, spec.FilePath), spec.Shared);

    public static async Task<IReadOnlyList<HookWriteWarning>> WriteHookConfigsAsync(
        IReadOnlyList<string> agentIds,
        IReadOnlyList<HookDeclaration> hooks,
        HookTargetResolver resolveTarget,
        CancellationToken ct = default)
    {
        var warnings = new List<HookWriteWarning>();
        if (hooks.Count == 0) return warnings;

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var id in agentIds)
        {
            var agent = AgentRegistry.GetAgent(id);
            if (agent is null) continue;

            if (agent.Hooks is null)
            {
                warnings.Add(new HookWriteWarning(id, $"""Agent "{agent.DisplayName}" does not support hooks"""));
                continue;
            }

            var serialized = agent.SerializeHooks!(hooks);
            var spec = agent.Hooks;
            var target = resolveTarget(id, spec);
            if (!seen.Add(target.FilePath)) continue;

            Directory.CreateDirectory(Path.GetDirectoryName(target.FilePath)!);

            if (target.Shared)
                await MergeWriteAsync(target.FilePath, spec, serialized, ct).ConfigureAwait(false);
            else
                await FreshWriteAsync(target.FilePath, spec, serialized, ct).ConfigureAwait(false);
        }

        return warnings;
    }

    public static async Task<IReadOnlyList<(string Agent, string Issue)>> VerifyHookConfigsAsync(
        IReadOnlyList<string> agentIds,
        IReadOnlyList<HookDeclaration> hooks,
        HookTargetResolver resolveTarget,
        CancellationToken ct = default)
    {
        if (hooks.Count == 0) return [];

        var issues = new List<(string, string)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var id in agentIds)
        {
            var agent = AgentRegistry.GetAgent(id);
            if (agent is null) continue;
            if (agent.Hooks is null) continue;

            var spec = agent.Hooks;
            var target = resolveTarget(id, spec);
            if (!seen.Add(target.FilePath)) continue;

            if (!File.Exists(target.FilePath))
            {
                issues.Add((id, $"Hook config missing: {target.FilePath}"));
                continue;
            }

            try
            {
                var existing = await ReadExistingAsync(target.FilePath, ct).ConfigureAwait(false);
                var hooksNode = existing[spec.RootKey];
                if (hooksNode is null or not JsonObject)
                    issues.Add((id, $"""Hook config missing "{spec.RootKey}" key in {target.FilePath}"""));
            }
            catch
            {
                issues.Add((id, $"Failed to read hook config: {target.FilePath}"));
            }
        }

        return issues;
    }

    // ── Internal helpers ─────────────────────────────────────────────────────

    private static async Task FreshWriteAsync(string filePath, HookConfigSpec spec, JsonNode serialized, CancellationToken ct)
    {
        var doc = new JsonObject();
        if (spec.ExtraFields is not null)
        {
            foreach (var (key, value) in spec.ExtraFields)
                doc[key] = value.DeepClone();
        }
        doc[spec.RootKey] = serialized;
        await WriteJsonAsync(filePath, doc, ct).ConfigureAwait(false);
    }

    private static async Task MergeWriteAsync(string filePath, HookConfigSpec spec, JsonNode serialized, CancellationToken ct)
    {
        var existing = File.Exists(filePath)
            ? await ReadExistingAsync(filePath, ct).ConfigureAwait(false)
            : new JsonObject();
        existing[spec.RootKey] = serialized;
        await WriteJsonAsync(filePath, existing, ct).ConfigureAwait(false);
    }

    private static async Task<JsonObject> ReadExistingAsync(string filePath, CancellationToken ct)
    {
        var raw = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        return JsonNode.Parse(raw)?.AsObject() ?? new JsonObject();
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static async Task WriteJsonAsync(string filePath, JsonObject doc, CancellationToken ct) =>
        await File.WriteAllTextAsync(filePath, doc.ToJsonString(JsonOptions) + "\n", ct).ConfigureAwait(false);
}
