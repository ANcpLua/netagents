namespace NetAgents.Tests.Config;

using AwesomeAssertions;
using NetAgents.Config;
using Xunit;

public class WriterTests : IAsyncLifetime
{
    private string _configPath = null!;
    private string _dir = null!;

    private CancellationToken CT => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_dir);
        _configPath = Path.Combine(_dir, "agents.toml");
        await File.WriteAllTextAsync(_configPath, ConfigWriter.GenerateDefaultConfig());
    }

    public async ValueTask DisposeAsync()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
        await ValueTask.CompletedTask;
    }

    [Fact]
    public async Task DefaultConfig_ProducesValidToml()
    {
        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        config.Version.Should().Be(1);
        config.Skills.Should().BeEmpty();
    }

    [Fact]
    public void DefaultConfig_DoesNotContainGitignore()
    {
        ConfigWriter.GenerateDefaultConfig().Should().NotContain("gitignore");
    }

    [Fact]
    public void DefaultConfig_DoesNotContainPin()
    {
        ConfigWriter.GenerateDefaultConfig().Should().NotContain("pin");
    }

    [Fact]
    public void DefaultConfig_IncludesAgents()
    {
        var content = ConfigWriter.GenerateDefaultConfig(new DefaultConfigOptions(["claude", "cursor"]));
        content.Should().Contain("agents = [\"claude\", \"cursor\"]");
    }

    [Fact]
    public void DefaultConfig_TrustAllowAll()
    {
        var content = ConfigWriter.GenerateDefaultConfig(new DefaultConfigOptions(
            Trust: new TrustConfig(true, [], [], [])));
        content.Should().Contain("[trust]");
        content.Should().Contain("allow_all = true");
    }

    [Fact]
    public void DefaultConfig_TrustRestrictions()
    {
        var content = ConfigWriter.GenerateDefaultConfig(new DefaultConfigOptions(
            Trust: new TrustConfig(false, ["anthropics"], ["owner/repo"], ["gitlab.example.com"])));
        content.Should().Contain("[trust]");
        Assert.Matches("github_orgs.*\"anthropics\"", content);
        Assert.Matches("github_repos.*\"owner/repo\"", content);
        Assert.Matches("git_domains.*\"gitlab\\.example\\.com\"", content);
        content.Should().NotContain("allow_all");
    }

    [Fact]
    public void DefaultConfig_OmitsTrustWhenNoRestrictions()
    {
        var content = ConfigWriter.GenerateDefaultConfig(new DefaultConfigOptions(
            Trust: new TrustConfig(false, [], [], [])));
        content.Should().NotContain("[trust]");
    }

    [Fact]
    public async Task DefaultConfig_RoundTripsAllOptions()
    {
        var content = ConfigWriter.GenerateDefaultConfig(new DefaultConfigOptions(
            ["claude"],
            new TrustConfig(false, ["my-org"], [], [])));
        await File.WriteAllTextAsync(_configPath, content, CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        config.Version.Should().Be(1);
        config.Agents.Should().BeEquivalentTo(["claude"]);
        config.Trust?.GithubOrgs.Should().BeEquivalentTo(["my-org"]);
    }

    [Fact]
    public void DefaultConfig_IncludesSkills()
    {
        var content = ConfigWriter.GenerateDefaultConfig(new DefaultConfigOptions(
            Skills: [new SkillEntry("dotagents", "getsentry/dotagents")]));
        content.Should().Contain("[[skills]]");
        content.Should().Contain("name = \"dotagents\"");
        content.Should().Contain("source = \"getsentry/dotagents\"");
    }

    [Fact]
    public void DefaultConfig_IncludesSkillRefAndPath()
    {
        var content = ConfigWriter.GenerateDefaultConfig(new DefaultConfigOptions(
            Skills: [new SkillEntry("my-skill", "org/repo", "v1.0.0", "skills/my-skill")]));
        content.Should().Contain("ref = \"v1.0.0\"");
        content.Should().Contain("path = \"skills/my-skill\"");
    }

    [Fact]
    public void DefaultConfig_NoSkillsWhenOmitted()
    {
        ConfigWriter.GenerateDefaultConfig().Should().NotContain("[[skills]]");
    }

    [Fact]
    public void DefaultConfig_NoSkillsWhenEmpty()
    {
        var content = ConfigWriter.GenerateDefaultConfig(new DefaultConfigOptions(Skills: []));
        content.Should().NotContain("[[skills]]");
    }

    [Fact]
    public async Task DefaultConfig_RoundTripsSkills()
    {
        var content = ConfigWriter.GenerateDefaultConfig(new DefaultConfigOptions(
            Skills:
            [
                new SkillEntry("dotagents", "getsentry/dotagents"),
                new SkillEntry("find-bugs", "getsentry/skills", "v2.0.0")
            ]));
        await File.WriteAllTextAsync(_configPath, content, CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        config.Skills.Count.Should().Be(2);
        var first = config.Skills[0].Should().BeOfType<RegularSkillDependency>().Which;
        first.Name.Should().Be("dotagents");
        var second = config.Skills[1].Should().BeOfType<RegularSkillDependency>().Which;
        second.Ref.Should().Be("v2.0.0");
    }

    [Fact]
    public async Task AddSkill_SourceOnly()
    {
        await ConfigWriter.AddSkillToConfigAsync(_configPath, "pdf", "anthropics/skills", ct: CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        var pdf = config.Skills.OfType<RegularSkillDependency>().FirstOrDefault(s => s.Name == "pdf");
        pdf.Should().NotBeNull();
        pdf!.Source.Should().Be("anthropics/skills");
    }

    [Fact]
    public async Task AddSkill_WithRef()
    {
        await ConfigWriter.AddSkillToConfigAsync(_configPath, "pdf", "anthropics/skills", "v1.0.0", ct: CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        var pdf = config.Skills.OfType<RegularSkillDependency>().First(s => s.Name == "pdf");
        pdf.Ref.Should().Be("v1.0.0");
    }

    [Fact]
    public async Task AddSkill_WithRefAndPath()
    {
        await ConfigWriter.AddSkillToConfigAsync(_configPath, "review",
            "git:https://example.com/repo.git", "main", "skills/review", CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        var review = config.Skills.OfType<RegularSkillDependency>().First(s => s.Name == "review");
        review.Source.Should().Be("git:https://example.com/repo.git");
        review.Path.Should().Be("skills/review");
    }

    [Fact]
    public async Task AddSkill_Multiple()
    {
        await ConfigWriter.AddSkillToConfigAsync(_configPath, "a", "org/repo-a", ct: CT);
        await ConfigWriter.AddSkillToConfigAsync(_configPath, "b", "org/repo-b", ct: CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        config.Skills.Count.Should().Be(2);
    }

    [Fact]
    public async Task AddSkill_PathSource()
    {
        await ConfigWriter.AddSkillToConfigAsync(_configPath, "my-skill", "path:.agents/skills/my-skill", ct: CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        var skill = config.Skills.OfType<RegularSkillDependency>().First(s => s.Name == "my-skill");
        skill.Source.Should().Be("path:.agents/skills/my-skill");
    }

    [Fact]
    public async Task RemoveSkill_ExistingSkill()
    {
        await ConfigWriter.AddSkillToConfigAsync(_configPath, "pdf", "anthropics/skills", "v1.0.0", ct: CT);
        await ConfigWriter.RemoveSkillFromConfigAsync(_configPath, "pdf", CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        config.Skills.OfType<RegularSkillDependency>().Should().NotContain(s => s.Name == "pdf");
    }

    [Fact]
    public async Task RemoveSkill_PreservesOthers()
    {
        await ConfigWriter.AddSkillToConfigAsync(_configPath, "a", "org/repo-a", ct: CT);
        await ConfigWriter.AddSkillToConfigAsync(_configPath, "b", "org/repo-b", ct: CT);
        await ConfigWriter.RemoveSkillFromConfigAsync(_configPath, "a", CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        config.Skills.OfType<RegularSkillDependency>().Should().NotContain(s => s.Name == "a");
        config.Skills.OfType<RegularSkillDependency>().Should().Contain(s => s.Name == "b");
    }

    [Fact]
    public async Task RemoveSkill_NoOpForNonExistent()
    {
        var before = await File.ReadAllTextAsync(_configPath, CT);
        await ConfigWriter.RemoveSkillFromConfigAsync(_configPath, "nope", CT);
        var after = await File.ReadAllTextAsync(_configPath, CT);
        after.Should().Be(before);
    }

    [Fact]
    public async Task AddWildcard_Basic()
    {
        await ConfigWriter.AddWildcardToConfigAsync(_configPath, "getsentry/skills", ct: CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        config.Skills.Should().ContainSingle();
        var dep = config.Skills[0].Should().BeOfType<WildcardSkillDependency>().Which;
        dep.Source.Should().Be("getsentry/skills");
    }

    [Fact]
    public async Task AddWildcard_WithRef()
    {
        await ConfigWriter.AddWildcardToConfigAsync(_configPath, "getsentry/skills", "v1.0.0", ct: CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        config.Skills[0].Ref.Should().Be("v1.0.0");
    }

    [Fact]
    public async Task AddWildcard_WithExclude()
    {
        await ConfigWriter.AddWildcardToConfigAsync(_configPath, "getsentry/skills",
            exclude: ["deprecated-skill"], ct: CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        var dep = config.Skills[0].Should().BeOfType<WildcardSkillDependency>().Which;
        dep.Exclude.Should().BeEquivalentTo(["deprecated-skill"]);
    }

    [Fact]
    public async Task AddWildcard_RoundTripsWithRegularSkill()
    {
        await ConfigWriter.AddWildcardToConfigAsync(_configPath, "getsentry/skills", ct: CT);
        await ConfigWriter.AddSkillToConfigAsync(_configPath, "pdf", "anthropics/skills", ct: CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        config.Skills.Count.Should().Be(2);
        config.Skills.Should().Contain(s => s is WildcardSkillDependency);
        config.Skills.OfType<RegularSkillDependency>().Should().Contain(s => s.Name == "pdf");
    }

    [Fact]
    public async Task AddExclude_CreatesExcludeList()
    {
        await ConfigWriter.AddWildcardToConfigAsync(_configPath, "getsentry/skills", ct: CT);
        await ConfigWriter.AddExcludeToWildcardAsync(_configPath, "getsentry/skills", "deprecated", CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        var dep = config.Skills[0].Should().BeOfType<WildcardSkillDependency>().Which;
        dep.Exclude.Should().Contain("deprecated");
    }

    [Fact]
    public async Task AddExclude_AppendsToExisting()
    {
        await ConfigWriter.AddWildcardToConfigAsync(_configPath, "getsentry/skills",
            exclude: ["old-skill"], ct: CT);
        await ConfigWriter.AddExcludeToWildcardAsync(_configPath, "getsentry/skills", "another-skill", CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        var dep = config.Skills[0].Should().BeOfType<WildcardSkillDependency>().Which;
        dep.Exclude.Should().Contain("old-skill");
        dep.Exclude.Should().Contain("another-skill");
    }

    [Fact]
    public async Task AddExclude_OnlyModifiesMatchingSource()
    {
        await ConfigWriter.AddWildcardToConfigAsync(_configPath, "getsentry/skills", ct: CT);
        await ConfigWriter.AddWildcardToConfigAsync(_configPath, "anthropics/skills", ct: CT);
        await ConfigWriter.AddExcludeToWildcardAsync(_configPath, "getsentry/skills", "my-skill", CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        var getsentry = config.Skills.OfType<WildcardSkillDependency>().First(s => s.Source == "getsentry/skills");
        var anthropics = config.Skills.OfType<WildcardSkillDependency>().First(s => s.Source == "anthropics/skills");
        getsentry.Exclude.Should().Contain("my-skill");
        anthropics.Exclude.Should().BeEmpty();
    }

    [Fact]
    public async Task AddMcp_StdioServer()
    {
        await ConfigWriter.AddMcpToConfigAsync(_configPath, new McpConfig(
            "github", "npx", ["-y", "@modelcontextprotocol/server-github"], null, null, ["GITHUB_TOKEN"]), CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        config.Mcp.Should().ContainSingle();
        var mcp = config.Mcp[0];
        mcp.Name.Should().Be("github");
        mcp.Command.Should().Be("npx");
        mcp.Args.Should().BeEquivalentTo(["-y", "@modelcontextprotocol/server-github"]);
        mcp.Env.Should().BeEquivalentTo(["GITHUB_TOKEN"]);
    }

    [Fact]
    public async Task AddMcp_HttpServerWithHeaders()
    {
        await ConfigWriter.AddMcpToConfigAsync(_configPath, new McpConfig(
            "remote", null, null, "https://mcp.example.com/sse",
            new Dictionary<string, string> { ["Authorization"] = "Bearer tok" }, []), CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        config.Mcp.Should().ContainSingle();
        var mcp = config.Mcp[0];
        mcp.Name.Should().Be("remote");
        mcp.Url.Should().Be("https://mcp.example.com/sse");
        mcp.Headers.Should().NotBeNull();
        mcp.Headers!["Authorization"].Should().Be("Bearer tok");
    }

    [Fact]
    public async Task AddMcp_MultipleServers()
    {
        await ConfigWriter.AddMcpToConfigAsync(_configPath, new McpConfig("a", "cmd-a", null, null, null, []), CT);
        await ConfigWriter.AddMcpToConfigAsync(_configPath,
            new McpConfig("b", null, null, "https://b.example.com", null, []), CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        config.Mcp.Count.Should().Be(2);
    }

    [Fact]
    public async Task RemoveMcp_ExistingServer()
    {
        await ConfigWriter.AddMcpToConfigAsync(_configPath, new McpConfig("github", "npx", null, null, null, []), CT);
        await ConfigWriter.RemoveMcpFromConfigAsync(_configPath, "github", CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        config.Mcp.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveMcp_PreservesOthers()
    {
        await ConfigWriter.AddMcpToConfigAsync(_configPath, new McpConfig("a", "cmd-a", null, null, null, []), CT);
        await ConfigWriter.AddMcpToConfigAsync(_configPath, new McpConfig("b", "cmd-b", null, null, null, []), CT);
        await ConfigWriter.RemoveMcpFromConfigAsync(_configPath, "a", CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        config.Mcp.Should().ContainSingle();
        config.Mcp[0].Name.Should().Be("b");
    }

    [Fact]
    public async Task RemoveMcp_NoOpForNonExistent()
    {
        var before = await File.ReadAllTextAsync(_configPath, CT);
        await ConfigWriter.RemoveMcpFromConfigAsync(_configPath, "nope", CT);
        var after = await File.ReadAllTextAsync(_configPath, CT);
        after.Should().Be(before);
    }

    [Fact]
    public async Task RemoveMcp_DoesNotAffectSkillsWithSameName()
    {
        await ConfigWriter.AddSkillToConfigAsync(_configPath, "github", "org/github-skill", ct: CT);
        await ConfigWriter.AddMcpToConfigAsync(_configPath, new McpConfig("github", "npx", null, null, null, []), CT);
        await ConfigWriter.RemoveMcpFromConfigAsync(_configPath, "github", CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        config.Mcp.Should().BeEmpty();
        config.Skills.OfType<RegularSkillDependency>().Should().Contain(s => s.Name == "github");
    }
}
