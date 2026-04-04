namespace NetAgents.Tests.Config;

using AwesomeAssertions;
using NetAgents.Config;
using Xunit;

// Mirrors dotagents/src/config/schema.test.ts — describe("agentsConfigSchema")
public class SchemaTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AgentsConfig Minimal()
    {
        return new AgentsConfig(1, RepositorySource.Github, null, null, [], [], [], [], null);
    }

    private static AgentsConfig WithSkill(string name, string source)
    {
        return Minimal() with { Skills = [new RegularSkillDependency(name, source, null, null)] };
    }

    private static AgentsConfig WithMcp(McpConfig mcp)
    {
        return Minimal() with { Mcp = [mcp] };
    }

    // ── Top-level parsing ─────────────────────────────────────────────────────

    [Fact]
    public void ParsesMinimalValidConfig()
    {
        var cfg = Minimal();
        AgentsConfigValidator.Validate(cfg);
        cfg.Version.Should().Be(1);
        cfg.Skills.Should().BeEmpty();
    }

    [Fact]
    public void DefaultsDefaultRepositorySourceToGithub()
    {
        Minimal().DefaultRepositorySource.Should().Be(RepositorySource.Github);
    }

    [Fact]
    public void AcceptsDefaultRepositorySourceGitlab()
    {
        var cfg = Minimal() with { DefaultRepositorySource = RepositorySource.Gitlab };
        cfg.DefaultRepositorySource.Should().Be(RepositorySource.Gitlab);
    }

    [Fact]
    public void ParsesFullConfigWithAllFields()
    {
        var cfg = Minimal() with
        {
            Project = new ProjectConfig("test-project"),
            Symlinks = new SymlinksConfig([".claude", ".cursor"]),
            Skills =
            [
                new RegularSkillDependency("pdf-processing", "anthropics/skills", "v1.0.0", null),
                new RegularSkillDependency("my-skill", "path:../shared/my-skill", null, null)
            ]
        };
        AgentsConfigValidator.Validate(cfg);
        cfg.Project?.Name.Should().Be("test-project");
        cfg.Symlinks?.Targets.Should().BeEquivalentTo([".claude", ".cursor"]);
        cfg.Skills.Count.Should().Be(2);
    }

    [Fact]
    public void RejectsInvalidVersion()
    {
        var cfg = Minimal() with { Version = 2 };
        Assert.Throws<ConfigException>(() => AgentsConfigValidator.Validate(cfg));
    }

    // ── Source specifiers ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("anthropics/skills")]
    [InlineData("anthropics/skills@v1.0.0")]
    [InlineData("anthropics/skills@abc123")]
    [InlineData("git:https://example.com/repo.git")]
    [InlineData("git:ssh://git@example.com/repo.git")]
    [InlineData("git:git@github.com:owner/repo.git")]
    [InlineData("git:/tmp/local-repo")]
    [InlineData("path:../relative/dir")]
    [InlineData("https://github.com/owner/repo")]
    [InlineData("https://github.com/owner/repo.git")]
    [InlineData("git@github.com:owner/repo.git")]
    [InlineData("https://gitlab.com/group/repo")]
    [InlineData("git@gitlab.com:group/repo.git")]
    [InlineData("https://gitlab.com/group/subgroup/repo")]
    public void AcceptsValidSource(string source)
    {
        SkillDependencyHelpers.IsValidSkillSource(source).Should().BeTrue($"expected '{source}' to be valid");
    }

    [Theory]
    [InlineData("git:--upload-pack=evil")]
    [InlineData("git:relative/path")]
    [InlineData("just-a-name")]
    [InlineData("-bad/repo")]
    [InlineData("owner/-bad")]
    [InlineData("a/b/c")]
    [InlineData("https://github.com/-bad/repo")]
    public void RejectsInvalidSource(string source)
    {
        SkillDependencyHelpers.IsValidSkillSource(source).Should().BeFalse($"expected '{source}' to be invalid");
    }

    // ── Skill name validation ─────────────────────────────────────────────────

    [Theory]
    [InlineData("pdf-processing")]
    [InlineData("my_skill")]
    [InlineData("skill.v2")]
    [InlineData("find-bugs")]
    public void AcceptsValidSkillNames(string name)
    {
        SkillDependencyHelpers.IsValidSkillName(name).Should().BeTrue();
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("../evil")]
    [InlineData("foo/bar")]
    [InlineData(".hidden")]
    [InlineData("-bad")]
    public void RejectsInvalidSkillNames(string name)
    {
        SkillDependencyHelpers.IsValidSkillName(name).Should().BeFalse();
    }

    [Fact]
    public void ValidatorRejectsSkillWithInvalidName()
    {
        var cfg = Minimal() with
        {
            Skills = [new RegularSkillDependency("-bad", "owner/repo", null, null)]
        };
        Assert.Throws<ConfigException>(() => AgentsConfigValidator.Validate(cfg));
    }

    // ── agents field ──────────────────────────────────────────────────────────

    [Fact]
    public void AgentsDefaultsToEmpty()
    {
        Minimal().Agents.Should().BeEmpty();
    }

    [Fact]
    public void AcceptsValidAgentIds()
    {
        var cfg = Minimal() with { Agents = ["claude", "cursor"] };
        cfg.Agents.Should().BeEquivalentTo(["claude", "cursor"]);
    }

    // ── mcp field ─────────────────────────────────────────────────────────────

    [Fact]
    public void McpDefaultsToEmpty()
    {
        Minimal().Mcp.Should().BeEmpty();
    }

    [Fact]
    public void AcceptsStdioMcpServer()
    {
        var cfg = WithMcp(new McpConfig("github", "npx", ["-y", "@mcp/server-github"], null, null, []));
        AgentsConfigValidator.Validate(cfg);
        cfg.Mcp[0].Name.Should().Be("github");
        cfg.Mcp[0].Command.Should().Be("npx");
    }

    [Fact]
    public void AcceptsHttpMcpServer()
    {
        var cfg = WithMcp(new McpConfig("remote", null, null, "https://mcp.example.com/sse", null, []));
        AgentsConfigValidator.Validate(cfg);
        cfg.Mcp[0].Url.Should().Be("https://mcp.example.com/sse");
    }

    [Fact]
    public void AcceptsMcpServerWithEnvVars()
    {
        var cfg = WithMcp(new McpConfig("gh", "npx", [], null, null, ["GITHUB_TOKEN"]));
        AgentsConfigValidator.Validate(cfg);
        cfg.Mcp[0].Env.Should().BeEquivalentTo(["GITHUB_TOKEN"]);
    }

    [Fact]
    public void AcceptsMcpServerWithHeaders()
    {
        var headers = new Dictionary<string, string> { ["Authorization"] = "Bearer tok" };
        var cfg = WithMcp(new McpConfig("r", null, null, "https://x.com", headers, []));
        AgentsConfigValidator.Validate(cfg);
    }

    [Fact]
    public void RejectsMcpWithBothCommandAndUrl()
    {
        var cfg = WithMcp(new McpConfig("bad", "x", null, "https://x.com", null, []));
        Assert.Throws<ConfigException>(() => AgentsConfigValidator.Validate(cfg));
    }

    [Fact]
    public void RejectsMcpWithNeitherCommandNorUrl()
    {
        var cfg = WithMcp(new McpConfig("bad", null, null, null, null, []));
        Assert.Throws<ConfigException>(() => AgentsConfigValidator.Validate(cfg));
    }

    [Fact]
    public void RejectsMcpWithEmptyName()
    {
        var cfg = WithMcp(new McpConfig("", "x", null, null, null, []));
        Assert.Throws<ConfigException>(() => AgentsConfigValidator.Validate(cfg));
    }

    // ── trust section ─────────────────────────────────────────────────────────

    [Fact]
    public void ParsesTrustWithAllFields()
    {
        var trust = new TrustConfig(false, ["getsentry", "anthropics"], ["external-org/one-approved"],
            ["git.corp.example.com"]);
        var cfg = Minimal() with { Trust = trust };
        AgentsConfigValidator.Validate(cfg);
        cfg.Trust.Should().Be(trust);
    }

    [Fact]
    public void TrustDefaultsForMissingArrays()
    {
        var trust = new TrustConfig(false, ["getsentry"], [], []);
        var cfg = Minimal() with { Trust = trust };
        AgentsConfigValidator.Validate(cfg);
        cfg.Trust!.GithubOrgs.Should().BeEquivalentTo(["getsentry"]);
        cfg.Trust.GithubRepos.Should().BeEmpty();
        cfg.Trust.GitDomains.Should().BeEmpty();
    }

    [Fact]
    public void ParsesAllowAllTrue()
    {
        var cfg = Minimal() with { Trust = new TrustConfig(true, [], [], []) };
        cfg.Trust?.AllowAll.Should().BeTrue();
    }

    [Fact]
    public void TrustIsNullWhenAbsent()
    {
        Minimal().Trust.Should().BeNull();
    }

    // ── backward compatibility ─────────────────────────────────────────────────

    [Fact]
    public void ParsesConfigWithoutAgentsOrMcpFields()
    {
        var cfg = Minimal() with { Skills = [new RegularSkillDependency("test", "owner/repo", null, null)] };
        AgentsConfigValidator.Validate(cfg);
        cfg.Agents.Should().BeEmpty();
        cfg.Mcp.Should().BeEmpty();
        cfg.Skills.Should().ContainSingle();
    }
}
