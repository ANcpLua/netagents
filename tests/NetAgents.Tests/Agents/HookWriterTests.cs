namespace NetAgents.Tests.Agents;

using System.Text.Json.Nodes;
using NetAgents.Agents;
using NetAgents.Config;
using Xunit;

public class HookWriterTests : IAsyncLifetime
{
    private static readonly HookDeclaration[] Hooks =
    [
        new(HookEvent.PreToolUse, "Bash", ".agents/hooks/block-rm.sh"),
        new(HookEvent.Stop, null, ".agents/hooks/check-tests.sh")
    ];

    private string _dir = null!;
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private HookTargetResolver Resolver => HookWriter.ProjectResolver(_dir);

    public async ValueTask InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_dir);
        await ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        await ValueTask.CompletedTask;
    }

    private async Task<JsonObject> ReadJson(params string[] parts)
    {
        var raw = await File.ReadAllTextAsync(Path.Combine(_dir, Path.Combine(parts)), Ct);
        return JsonNode.Parse(raw)!.AsObject();
    }

    // ── toHookDeclarations ───────────────────────────────────────────────────

    [Fact]
    public void ToHookDeclarations_ConvertsConfigs()
    {
        HookConfig[] configs =
        [
            new(HookEvent.PreToolUse, "Bash", ".agents/hooks/block-rm.sh"),
            new(HookEvent.Stop, null, ".agents/hooks/check-tests.sh")
        ];
        var decls = HookWriter.ToHookDeclarations(configs);
        Assert.Equal(2, decls.Count);
        Assert.Equal(HookEvent.PreToolUse, decls[0].Event);
        Assert.Equal("Bash", decls[0].Matcher);
        Assert.Null(decls[1].Matcher);
    }

    // ── write tests ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SkipsWhenNoHooks()
    {
        var warnings = await HookWriter.WriteHookConfigsAsync(["claude"], [], Resolver, Ct);
        Assert.Empty(warnings);
        Assert.False(File.Exists(Path.Combine(_dir, ".claude", "settings.json")));
    }

    [Fact]
    public async Task WritesClaudeSettingsJson()
    {
        await HookWriter.WriteHookConfigsAsync(["claude"], Hooks, Resolver, Ct);

        var doc = await ReadJson(".claude", "settings.json");
        var pre = doc["hooks"]!["PreToolUse"]!.AsArray();
        Assert.Single(pre);
        Assert.Equal("Bash", pre[0]!["matcher"]!.GetValue<string>());

        var stop = doc["hooks"]!["Stop"]!.AsArray();
        Assert.Single(stop);
    }

    [Fact]
    public async Task WritesCursorHooksWithVersion()
    {
        await HookWriter.WriteHookConfigsAsync(["cursor"], Hooks, Resolver, Ct);

        var doc = await ReadJson(".cursor", "hooks.json");
        Assert.Equal(1, doc["version"]!.GetValue<int>());
        Assert.NotNull(doc["hooks"]!["beforeShellExecution"]);
        Assert.NotNull(doc["hooks"]!["beforeMCPExecution"]);
        Assert.NotNull(doc["hooks"]!["stop"]);
    }

    [Fact]
    public async Task CursorDropsMatcher()
    {
        await HookWriter.WriteHookConfigsAsync(["cursor"], Hooks, Resolver, Ct);

        var doc = await ReadJson(".cursor", "hooks.json");
        foreach (var prop in doc["hooks"]!.AsObject())
        foreach (var entry in prop.Value!.AsArray())
            Assert.Null(entry!.AsObject()["matcher"]);
    }

    [Fact]
    public async Task VsCodeWritesToClaudeSettings()
    {
        await HookWriter.WriteHookConfigsAsync(["vscode"], Hooks, Resolver, Ct);
        Assert.True(File.Exists(Path.Combine(_dir, ".claude", "settings.json")));
    }

    [Fact]
    public async Task DeduplicatesSharedFileBetweenClaudeAndVsCode()
    {
        await HookWriter.WriteHookConfigsAsync(["claude", "vscode"], Hooks, Resolver, Ct);

        var doc = await ReadJson(".claude", "settings.json");
        Assert.Single(doc["hooks"]!["PreToolUse"]!.AsArray());
    }

    [Fact]
    public async Task ReturnsWarningsForUnsupportedAgents()
    {
        var warnings = await HookWriter.WriteHookConfigsAsync(["codex", "opencode"], Hooks, Resolver, Ct);
        Assert.Equal(2, warnings.Count);
        Assert.Equal("codex", warnings[0].Agent);
        Assert.Equal("opencode", warnings[1].Agent);
        Assert.Contains("does not support", warnings[0].Message);
    }

    [Fact]
    public async Task WritesSupportedAndWarnsUnsupported()
    {
        var warnings = await HookWriter.WriteHookConfigsAsync(["claude", "codex"], Hooks, Resolver, Ct);
        Assert.Single(warnings);
        Assert.Equal("codex", warnings[0].Agent);
        Assert.True(File.Exists(Path.Combine(_dir, ".claude", "settings.json")));
    }

    [Fact]
    public async Task MergesIntoExistingSharedConfig()
    {
        var claudeDir = Path.Combine(_dir, ".claude");
        Directory.CreateDirectory(claudeDir);
        await File.WriteAllTextAsync(
            Path.Combine(claudeDir, "settings.json"),
            """{"permissions": {"allow": ["Read"]}}""", Ct);

        await HookWriter.WriteHookConfigsAsync(["claude"], Hooks, Resolver, Ct);

        var doc = await ReadJson(".claude", "settings.json");
        Assert.NotNull(doc["permissions"]);
        Assert.NotNull(doc["hooks"]);
    }

    [Fact]
    public async Task IsIdempotent()
    {
        await HookWriter.WriteHookConfigsAsync(["claude"], Hooks, Resolver, Ct);
        var first = await File.ReadAllTextAsync(Path.Combine(_dir, ".claude", "settings.json"), Ct);

        await HookWriter.WriteHookConfigsAsync(["claude"], Hooks, Resolver, Ct);
        var second = await File.ReadAllTextAsync(Path.Combine(_dir, ".claude", "settings.json"), Ct);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task HandlesMultipleAgentsIncludingCursor()
    {
        await HookWriter.WriteHookConfigsAsync(["claude", "cursor"], Hooks, Resolver, Ct);
        Assert.True(File.Exists(Path.Combine(_dir, ".claude", "settings.json")));
        Assert.True(File.Exists(Path.Combine(_dir, ".cursor", "hooks.json")));
    }

    // ── verify tests ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Verify_NoIssuesWhenConfigsMatch()
    {
        await HookWriter.WriteHookConfigsAsync(["claude"], Hooks, Resolver, Ct);
        var issues = await HookWriter.VerifyHookConfigsAsync(["claude"], Hooks, Resolver, Ct);
        Assert.Empty(issues);
    }

    [Fact]
    public async Task Verify_ReportsMissingConfigFile()
    {
        var issues = await HookWriter.VerifyHookConfigsAsync(["claude"], Hooks, Resolver, Ct);
        Assert.Single(issues);
        Assert.Contains("missing", issues[0].Issue);
    }

    [Fact]
    public async Task Verify_SkipsUnsupportedAgents()
    {
        var issues = await HookWriter.VerifyHookConfigsAsync(["codex"], Hooks, Resolver, Ct);
        Assert.Empty(issues);
    }

    [Fact]
    public async Task Verify_EmptyWhenNoHooks()
    {
        var issues = await HookWriter.VerifyHookConfigsAsync(["claude"], [], Resolver, Ct);
        Assert.Empty(issues);
    }

    [Fact]
    public async Task Verify_ReportsMissingHooksKey()
    {
        var claudeDir = Path.Combine(_dir, ".claude");
        Directory.CreateDirectory(claudeDir);
        await File.WriteAllTextAsync(
            Path.Combine(claudeDir, "settings.json"),
            """{"permissions": {}}""", Ct);

        var issues = await HookWriter.VerifyHookConfigsAsync(["claude"], Hooks, Resolver, Ct);
        Assert.Single(issues);
        Assert.Contains("hooks", issues[0].Issue);
    }
}
