namespace NetAgents.Tests.Agents;

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
        Assert.Equal(Path.Combine(Home, ".claude.json"), t.FilePath);
        Assert.True(t.Shared);
    }

    [Fact]
    public void Cursor_TargetsCursorMcpJsonNotShared()
    {
        var t = AgentPaths.GetUserMcpTarget("cursor");
        Assert.Equal(Path.Combine(Home, ".cursor", "mcp.json"), t.FilePath);
        Assert.False(t.Shared);
    }

    [Fact]
    public void Codex_TargetsCodexConfigTomlShared()
    {
        var t = AgentPaths.GetUserMcpTarget("codex");
        Assert.Equal(Path.Combine(Home, ".codex", "config.toml"), t.FilePath);
        Assert.True(t.Shared);
    }

    [Fact]
    public void VsCode_TargetsPlatformSpecificNotShared()
    {
        var t = AgentPaths.GetUserMcpTarget("vscode");
        Assert.False(t.Shared);
        Assert.EndsWith("mcp.json", t.FilePath);
    }

    [Fact]
    public void OpenCode_TargetsConfigDirShared()
    {
        var t = AgentPaths.GetUserMcpTarget("opencode");
        Assert.Equal(Path.Combine(Home, ".config", "opencode", "opencode.json"), t.FilePath);
        Assert.True(t.Shared);
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
        Assert.Equal(".claude", agent.SkillsParentDir);
        Assert.Equal([Path.Combine(Home, ".claude")], agent.UserSkillsParentDirs);
    }

    [Fact]
    public void Cursor_SharesClaudeSkillsSymlink()
    {
        var agent = AgentRegistry.GetAgent("cursor")!;
        Assert.Equal(".claude", agent.SkillsParentDir);
        Assert.Equal([Path.Combine(Home, ".claude")], agent.UserSkillsParentDirs);
    }

    [Fact]
    public void VsCode_ReadsAgentsNatively()
    {
        var agent = AgentRegistry.GetAgent("vscode")!;
        Assert.Null(agent.SkillsParentDir);
        Assert.Null(agent.UserSkillsParentDirs);
    }

    [Fact]
    public void Codex_ReadsAgentsNatively()
    {
        var agent = AgentRegistry.GetAgent("codex")!;
        Assert.Null(agent.SkillsParentDir);
        Assert.Null(agent.UserSkillsParentDirs);
    }

    [Fact]
    public void OpenCode_ReadsAgentsNatively()
    {
        var agent = AgentRegistry.GetAgent("opencode")!;
        Assert.Null(agent.SkillsParentDir);
        Assert.Null(agent.UserSkillsParentDirs);
    }
}
