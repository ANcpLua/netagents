namespace NetAgents.Tests.Agents;

using AwesomeAssertions;
using NetAgents.Agents;
using Xunit;

public class PathsTests
{
    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // ── getUserMcpTarget ─────────────────────────────────────────────────────

    [Fact]
    public void Claude_TargetsClaudeJsonShared()
    {
        var t = AgentPaths.GetUserMcpTarget("claude");
        t.FilePath.Should().Be(Path.Combine(Home, ".claude.json"));
        t.Shared.Should().BeTrue();
    }

    [Fact]
    public void Cursor_TargetsCursorMcpJsonNotShared()
    {
        var t = AgentPaths.GetUserMcpTarget("cursor");
        t.FilePath.Should().Be(Path.Combine(Home, ".cursor", "mcp.json"));
        t.Shared.Should().BeFalse();
    }

    [Fact]
    public void Codex_TargetsCodexConfigTomlShared()
    {
        var t = AgentPaths.GetUserMcpTarget("codex");
        t.FilePath.Should().Be(Path.Combine(Home, ".codex", "config.toml"));
        t.Shared.Should().BeTrue();
    }

    [Fact]
    public void VsCode_TargetsPlatformSpecificNotShared()
    {
        var t = AgentPaths.GetUserMcpTarget("vscode");
        t.Shared.Should().BeFalse();
        t.FilePath.Should().EndWith("mcp.json");
    }

    [Fact]
    public void OpenCode_TargetsConfigDirShared()
    {
        var t = AgentPaths.GetUserMcpTarget("opencode");
        t.FilePath.Should().Be(Path.Combine(Home, ".config", "opencode", "opencode.json"));
        t.Shared.Should().BeTrue();
    }

    [Fact]
    public void ThrowsForUnknownAgent()
    {
        Assert.Throws<ArgumentException>(() => AgentPaths.GetUserMcpTarget("emacs"));
    }

    // ── skill discovery paths ────────────────────────────────────────────────

    [Fact]
    public void Claude_NeedsProjectAndUserSymlinks()
    {
        var agent = AgentRegistry.GetAgent("claude")!;
        agent.SkillsParentDir.Should().Be(".claude");
        agent.UserSkillsParentDirs.Should().BeEquivalentTo([Path.Combine(Home, ".claude")]);
    }

    [Fact]
    public void Cursor_SharesClaudeSkillsSymlink()
    {
        var agent = AgentRegistry.GetAgent("cursor")!;
        agent.SkillsParentDir.Should().Be(".claude");
        agent.UserSkillsParentDirs.Should().BeEquivalentTo([Path.Combine(Home, ".claude")]);
    }

    [Fact]
    public void VsCode_ReadsAgentsNatively()
    {
        var agent = AgentRegistry.GetAgent("vscode")!;
        agent.SkillsParentDir.Should().BeNull();
        agent.UserSkillsParentDirs.Should().BeNull();
    }

    [Fact]
    public void Codex_ReadsAgentsNatively()
    {
        var agent = AgentRegistry.GetAgent("codex")!;
        agent.SkillsParentDir.Should().BeNull();
        agent.UserSkillsParentDirs.Should().BeNull();
    }

    [Fact]
    public void OpenCode_ReadsAgentsNatively()
    {
        var agent = AgentRegistry.GetAgent("opencode")!;
        agent.SkillsParentDir.Should().BeNull();
        agent.UserSkillsParentDirs.Should().BeNull();
    }
}
