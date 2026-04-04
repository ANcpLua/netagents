namespace NetAgents.Tests.Agents;

using System.Text.Json.Nodes;
using AwesomeAssertions;
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
        decls.Count.Should().Be(2);
        decls[0].Event.Should().Be(HookEvent.PreToolUse);
        decls[0].Matcher.Should().Be("Bash");
        decls[1].Matcher.Should().BeNull();
    }

    // ── write tests ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SkipsWhenNoHooks()
    {
        var warnings = await HookWriter.WriteHookConfigsAsync(["claude"], [], Resolver, Ct);
        warnings.Should().BeEmpty();
        File.Exists(Path.Combine(_dir, ".claude", "settings.json")).Should().BeFalse();
    }

    [Fact]
    public async Task WritesClaudeSettingsJson()
    {
        await HookWriter.WriteHookConfigsAsync(["claude"], Hooks, Resolver, Ct);

        var doc = await ReadJson(".claude", "settings.json");
        var pre = doc["hooks"]!["PreToolUse"]!.AsArray();
        pre.Should().ContainSingle();
        pre[0]!["matcher"]!.GetValue<string>().Should().Be("Bash");

        var stop = doc["hooks"]!["Stop"]!.AsArray();
        stop.Should().ContainSingle();
    }

    [Fact]
    public async Task WritesCursorHooksWithVersion()
    {
        await HookWriter.WriteHookConfigsAsync(["cursor"], Hooks, Resolver, Ct);

        var doc = await ReadJson(".cursor", "hooks.json");
        doc["version"]!.GetValue<int>().Should().Be(1);
        doc["hooks"]!["beforeShellExecution"].Should().NotBeNull();
        doc["hooks"]!["beforeMCPExecution"].Should().NotBeNull();
        doc["hooks"]!["stop"].Should().NotBeNull();
    }

    [Fact]
    public async Task CursorDropsMatcher()
    {
        await HookWriter.WriteHookConfigsAsync(["cursor"], Hooks, Resolver, Ct);

        var doc = await ReadJson(".cursor", "hooks.json");
        foreach (var prop in doc["hooks"]!.AsObject())
        foreach (var entry in prop.Value!.AsArray())
            entry!.AsObject()["matcher"].Should().BeNull();
    }

    [Fact]
    public async Task VsCodeWritesToClaudeSettings()
    {
        await HookWriter.WriteHookConfigsAsync(["vscode"], Hooks, Resolver, Ct);
        File.Exists(Path.Combine(_dir, ".claude", "settings.json")).Should().BeTrue();
    }

    [Fact]
    public async Task DeduplicatesSharedFileBetweenClaudeAndVsCode()
    {
        await HookWriter.WriteHookConfigsAsync(["claude", "vscode"], Hooks, Resolver, Ct);

        var doc = await ReadJson(".claude", "settings.json");
        doc["hooks"]!["PreToolUse"]!.AsArray().Should().ContainSingle();
    }

    [Fact]
    public async Task ReturnsWarningsForUnsupportedAgents()
    {
        var warnings = await HookWriter.WriteHookConfigsAsync(["codex", "opencode"], Hooks, Resolver, Ct);
        warnings.Count.Should().Be(2);
        warnings[0].Agent.Should().Be("codex");
        warnings[1].Agent.Should().Be("opencode");
        warnings[0].Message.Should().Contain("does not support");
    }

    [Fact]
    public async Task WritesSupportedAndWarnsUnsupported()
    {
        var warnings = await HookWriter.WriteHookConfigsAsync(["claude", "codex"], Hooks, Resolver, Ct);
        warnings.Should().ContainSingle();
        warnings[0].Agent.Should().Be("codex");
        File.Exists(Path.Combine(_dir, ".claude", "settings.json")).Should().BeTrue();
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
        doc["permissions"].Should().NotBeNull();
        doc["hooks"].Should().NotBeNull();
    }

    [Fact]
    public async Task IsIdempotent()
    {
        await HookWriter.WriteHookConfigsAsync(["claude"], Hooks, Resolver, Ct);
        var first = await File.ReadAllTextAsync(Path.Combine(_dir, ".claude", "settings.json"), Ct);

        await HookWriter.WriteHookConfigsAsync(["claude"], Hooks, Resolver, Ct);
        var second = await File.ReadAllTextAsync(Path.Combine(_dir, ".claude", "settings.json"), Ct);

        second.Should().Be(first);
    }

    [Fact]
    public async Task HandlesMultipleAgentsIncludingCursor()
    {
        await HookWriter.WriteHookConfigsAsync(["claude", "cursor"], Hooks, Resolver, Ct);
        File.Exists(Path.Combine(_dir, ".claude", "settings.json")).Should().BeTrue();
        File.Exists(Path.Combine(_dir, ".cursor", "hooks.json")).Should().BeTrue();
    }

    // ── verify tests ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Verify_NoIssuesWhenConfigsMatch()
    {
        await HookWriter.WriteHookConfigsAsync(["claude"], Hooks, Resolver, Ct);
        var issues = await HookWriter.VerifyHookConfigsAsync(["claude"], Hooks, Resolver, Ct);
        issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Verify_ReportsMissingConfigFile()
    {
        var issues = await HookWriter.VerifyHookConfigsAsync(["claude"], Hooks, Resolver, Ct);
        issues.Should().ContainSingle();
        issues[0].Issue.Should().Contain("missing");
    }

    [Fact]
    public async Task Verify_SkipsUnsupportedAgents()
    {
        var issues = await HookWriter.VerifyHookConfigsAsync(["codex"], Hooks, Resolver, Ct);
        issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Verify_EmptyWhenNoHooks()
    {
        var issues = await HookWriter.VerifyHookConfigsAsync(["claude"], [], Resolver, Ct);
        issues.Should().BeEmpty();
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
        issues.Should().ContainSingle();
        issues[0].Issue.Should().Contain("hooks");
    }
}
