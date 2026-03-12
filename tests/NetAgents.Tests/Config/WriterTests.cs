using NetAgents.Config;
using Xunit;

namespace NetAgents.Tests.Config;

public class WriterTests : IAsyncLifetime
{
    private string _dir = null!;
    private string _configPath = null!;

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
            Directory.Delete(_dir, recursive: true);
        await ValueTask.CompletedTask;
    }

    private CancellationToken CT => TestContext.Current.CancellationToken;

    // ── GenerateDefaultConfig ────────────────────────────────────────────────────

    [Fact]
    public async Task DefaultConfig_ProducesValidToml()
    {
        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        Assert.Equal(1, config.Version);
        Assert.Empty(config.Skills);
    }

    [Fact]
    public void DefaultConfig_DoesNotContainGitignore()
    {
        Assert.DoesNotContain("gitignore", ConfigWriter.GenerateDefaultConfig());
    }

    [Fact]
    public void DefaultConfig_DoesNotContainPin()
    {
        Assert.DoesNotContain("pin", ConfigWriter.GenerateDefaultConfig());
    }

    [Fact]
    public void DefaultConfig_IncludesAgents()
    {
        var content = ConfigWriter.GenerateDefaultConfig(new DefaultConfigOptions(Agents: ["claude", "cursor"]));
        Assert.Contains("agents = [\"claude\", \"cursor\"]", content);
    }

    [Fact]
    public void DefaultConfig_TrustAllowAll()
    {
        var content = ConfigWriter.GenerateDefaultConfig(new DefaultConfigOptions(
            Trust: new TrustConfig(true, [], [], [])));
        Assert.Contains("[trust]", content);
        Assert.Contains("allow_all = true", content);
    }

    [Fact]
    public void DefaultConfig_TrustRestrictions()
    {
        var content = ConfigWriter.GenerateDefaultConfig(new DefaultConfigOptions(
            Trust: new TrustConfig(false, ["anthropics"], ["owner/repo"], ["gitlab.example.com"])));
        Assert.Contains("[trust]", content);
        Assert.Matches("github_orgs.*\"anthropics\"", content);
        Assert.Matches("github_repos.*\"owner/repo\"", content);
        Assert.Matches("git_domains.*\"gitlab\\.example\\.com\"", content);
        Assert.DoesNotContain("allow_all", content);
    }

    [Fact]
    public void DefaultConfig_OmitsTrustWhenNoRestrictions()
    {
        var content = ConfigWriter.GenerateDefaultConfig(new DefaultConfigOptions(
            Trust: new TrustConfig(false, [], [], [])));
        Assert.DoesNotContain("[trust]", content);
    }

    [Fact]
    public async Task DefaultConfig_RoundTripsAllOptions()
    {
        var content = ConfigWriter.GenerateDefaultConfig(new DefaultConfigOptions(
            Agents: ["claude"],
            Trust: new TrustConfig(false, ["my-org"], [], [])));
        await File.WriteAllTextAsync(_configPath, content, CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        Assert.Equal(1, config.Version);
        Assert.Equal(["claude"], config.Agents);
        Assert.Equal(["my-org"], config.Trust?.GithubOrgs);
    }

    [Fact]
    public void DefaultConfig_IncludesSkills()
    {
        var content = ConfigWriter.GenerateDefaultConfig(new DefaultConfigOptions(
            Skills: [new SkillEntry("dotagents", "getsentry/dotagents")]));
        Assert.Contains("[[skills]]", content);
        Assert.Contains("name = \"dotagents\"", content);
        Assert.Contains("source = \"getsentry/dotagents\"", content);
    }

    [Fact]
    public void DefaultConfig_IncludesSkillRefAndPath()
    {
        var content = ConfigWriter.GenerateDefaultConfig(new DefaultConfigOptions(
            Skills: [new SkillEntry("my-skill", "org/repo", Ref: "v1.0.0", Path: "skills/my-skill")]));
        Assert.Contains("ref = \"v1.0.0\"", content);
        Assert.Contains("path = \"skills/my-skill\"", content);
    }

    [Fact]
    public void DefaultConfig_NoSkillsWhenOmitted()
    {
        Assert.DoesNotContain("[[skills]]", ConfigWriter.GenerateDefaultConfig());
    }

    [Fact]
    public void DefaultConfig_NoSkillsWhenEmpty()
    {
        var content = ConfigWriter.GenerateDefaultConfig(new DefaultConfigOptions(Skills: []));
        Assert.DoesNotContain("[[skills]]", content);
    }

    [Fact]
    public async Task DefaultConfig_RoundTripsSkills()
    {
        var content = ConfigWriter.GenerateDefaultConfig(new DefaultConfigOptions(
            Skills:
            [
                new SkillEntry("dotagents", "getsentry/dotagents"),
                new SkillEntry("find-bugs", "getsentry/skills", Ref: "v2.0.0"),
            ]));
        await File.WriteAllTextAsync(_configPath, content, CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        Assert.Equal(2, config.Skills.Count);
        var first = Assert.IsType<RegularSkillDependency>(config.Skills[0]);
        Assert.Equal("dotagents", first.Name);
        var second = Assert.IsType<RegularSkillDependency>(config.Skills[1]);
        Assert.Equal("v2.0.0", second.Ref);
    }

    // ── AddSkillToConfig ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AddSkill_SourceOnly()
    {
        await ConfigWriter.AddSkillToConfigAsync(_configPath, "pdf", "anthropics/skills", ct: CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        var pdf = config.Skills.OfType<RegularSkillDependency>().FirstOrDefault(s => s.Name == "pdf");
        Assert.NotNull(pdf);
        Assert.Equal("anthropics/skills", pdf.Source);
    }

    [Fact]
    public async Task AddSkill_WithRef()
    {
        await ConfigWriter.AddSkillToConfigAsync(_configPath, "pdf", "anthropics/skills", @ref: "v1.0.0", ct: CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        var pdf = config.Skills.OfType<RegularSkillDependency>().First(s => s.Name == "pdf");
        Assert.Equal("v1.0.0", pdf.Ref);
    }

    [Fact]
    public async Task AddSkill_WithRefAndPath()
    {
        await ConfigWriter.AddSkillToConfigAsync(_configPath, "review",
            "git:https://example.com/repo.git", @ref: "main", path: "skills/review", ct: CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        var review = config.Skills.OfType<RegularSkillDependency>().First(s => s.Name == "review");
        Assert.Equal("git:https://example.com/repo.git", review.Source);
        Assert.Equal("skills/review", review.Path);
    }

    [Fact]
    public async Task AddSkill_Multiple()
    {
        await ConfigWriter.AddSkillToConfigAsync(_configPath, "a", "org/repo-a", ct: CT);
        await ConfigWriter.AddSkillToConfigAsync(_configPath, "b", "org/repo-b", ct: CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        Assert.Equal(2, config.Skills.Count);
    }

    [Fact]
    public async Task AddSkill_PathSource()
    {
        await ConfigWriter.AddSkillToConfigAsync(_configPath, "my-skill", "path:.agents/skills/my-skill", ct: CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        var skill = config.Skills.OfType<RegularSkillDependency>().First(s => s.Name == "my-skill");
        Assert.Equal("path:.agents/skills/my-skill", skill.Source);
    }

    // ── RemoveSkillFromConfig ────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveSkill_ExistingSkill()
    {
        await ConfigWriter.AddSkillToConfigAsync(_configPath, "pdf", "anthropics/skills", @ref: "v1.0.0", ct: CT);
        await ConfigWriter.RemoveSkillFromConfigAsync(_configPath, "pdf", CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        Assert.DoesNotContain(config.Skills.OfType<RegularSkillDependency>(), s => s.Name == "pdf");
    }

    [Fact]
    public async Task RemoveSkill_PreservesOthers()
    {
        await ConfigWriter.AddSkillToConfigAsync(_configPath, "a", "org/repo-a", ct: CT);
        await ConfigWriter.AddSkillToConfigAsync(_configPath, "b", "org/repo-b", ct: CT);
        await ConfigWriter.RemoveSkillFromConfigAsync(_configPath, "a", CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        Assert.DoesNotContain(config.Skills.OfType<RegularSkillDependency>(), s => s.Name == "a");
        Assert.Contains(config.Skills.OfType<RegularSkillDependency>(), s => s.Name == "b");
    }

    [Fact]
    public async Task RemoveSkill_NoOpForNonExistent()
    {
        var before = await File.ReadAllTextAsync(_configPath, CT);
        await ConfigWriter.RemoveSkillFromConfigAsync(_configPath, "nope", CT);
        var after = await File.ReadAllTextAsync(_configPath, CT);
        Assert.Equal(before, after);
    }

    // ── AddWildcardToConfig ──────────────────────────────────────────────────────

    [Fact]
    public async Task AddWildcard_Basic()
    {
        await ConfigWriter.AddWildcardToConfigAsync(_configPath, "getsentry/skills", ct: CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        Assert.Single(config.Skills);
        var dep = Assert.IsType<WildcardSkillDependency>(config.Skills[0]);
        Assert.Equal("getsentry/skills", dep.Source);
    }

    [Fact]
    public async Task AddWildcard_WithRef()
    {
        await ConfigWriter.AddWildcardToConfigAsync(_configPath, "getsentry/skills", @ref: "v1.0.0", ct: CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        Assert.Equal("v1.0.0", config.Skills[0].Ref);
    }

    [Fact]
    public async Task AddWildcard_WithExclude()
    {
        await ConfigWriter.AddWildcardToConfigAsync(_configPath, "getsentry/skills",
            exclude: ["deprecated-skill"], ct: CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        var dep = Assert.IsType<WildcardSkillDependency>(config.Skills[0]);
        Assert.Equal(["deprecated-skill"], dep.Exclude);
    }

    [Fact]
    public async Task AddWildcard_RoundTripsWithRegularSkill()
    {
        await ConfigWriter.AddWildcardToConfigAsync(_configPath, "getsentry/skills", ct: CT);
        await ConfigWriter.AddSkillToConfigAsync(_configPath, "pdf", "anthropics/skills", ct: CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        Assert.Equal(2, config.Skills.Count);
        Assert.Contains(config.Skills, s => s is WildcardSkillDependency);
        Assert.Contains(config.Skills.OfType<RegularSkillDependency>(), s => s.Name == "pdf");
    }

    // ── AddExcludeToWildcard ─────────────────────────────────────────────────────

    [Fact]
    public async Task AddExclude_CreatesExcludeList()
    {
        await ConfigWriter.AddWildcardToConfigAsync(_configPath, "getsentry/skills", ct: CT);
        await ConfigWriter.AddExcludeToWildcardAsync(_configPath, "getsentry/skills", "deprecated", CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        var dep = Assert.IsType<WildcardSkillDependency>(config.Skills[0]);
        Assert.Contains("deprecated", dep.Exclude);
    }

    [Fact]
    public async Task AddExclude_AppendsToExisting()
    {
        await ConfigWriter.AddWildcardToConfigAsync(_configPath, "getsentry/skills",
            exclude: ["old-skill"], ct: CT);
        await ConfigWriter.AddExcludeToWildcardAsync(_configPath, "getsentry/skills", "another-skill", CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        var dep = Assert.IsType<WildcardSkillDependency>(config.Skills[0]);
        Assert.Contains("old-skill", dep.Exclude);
        Assert.Contains("another-skill", dep.Exclude);
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
        Assert.Contains("my-skill", getsentry.Exclude);
        Assert.Empty(anthropics.Exclude);
    }

    // ── AddMcpToConfig ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AddMcp_StdioServer()
    {
        await ConfigWriter.AddMcpToConfigAsync(_configPath, new McpConfig(
            "github", "npx", ["-y", "@modelcontextprotocol/server-github"], null, null, ["GITHUB_TOKEN"]), CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        Assert.Single(config.Mcp);
        var mcp = config.Mcp[0];
        Assert.Equal("github", mcp.Name);
        Assert.Equal("npx", mcp.Command);
        Assert.Equal(["-y", "@modelcontextprotocol/server-github"], mcp.Args);
        Assert.Equal(["GITHUB_TOKEN"], mcp.Env);
    }

    [Fact]
    public async Task AddMcp_HttpServerWithHeaders()
    {
        await ConfigWriter.AddMcpToConfigAsync(_configPath, new McpConfig(
            "remote", null, null, "https://mcp.example.com/sse",
            new Dictionary<string, string> { ["Authorization"] = "Bearer tok" }, []), CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        Assert.Single(config.Mcp);
        var mcp = config.Mcp[0];
        Assert.Equal("remote", mcp.Name);
        Assert.Equal("https://mcp.example.com/sse", mcp.Url);
        Assert.NotNull(mcp.Headers);
        Assert.Equal("Bearer tok", mcp.Headers["Authorization"]);
    }

    [Fact]
    public async Task AddMcp_MultipleServers()
    {
        await ConfigWriter.AddMcpToConfigAsync(_configPath, new McpConfig("a", "cmd-a", null, null, null, []), CT);
        await ConfigWriter.AddMcpToConfigAsync(_configPath, new McpConfig("b", null, null, "https://b.example.com", null, []), CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        Assert.Equal(2, config.Mcp.Count);
    }

    // ── RemoveMcpFromConfig ──────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveMcp_ExistingServer()
    {
        await ConfigWriter.AddMcpToConfigAsync(_configPath, new McpConfig("github", "npx", null, null, null, []), CT);
        await ConfigWriter.RemoveMcpFromConfigAsync(_configPath, "github", CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        Assert.Empty(config.Mcp);
    }

    [Fact]
    public async Task RemoveMcp_PreservesOthers()
    {
        await ConfigWriter.AddMcpToConfigAsync(_configPath, new McpConfig("a", "cmd-a", null, null, null, []), CT);
        await ConfigWriter.AddMcpToConfigAsync(_configPath, new McpConfig("b", "cmd-b", null, null, null, []), CT);
        await ConfigWriter.RemoveMcpFromConfigAsync(_configPath, "a", CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        Assert.Single(config.Mcp);
        Assert.Equal("b", config.Mcp[0].Name);
    }

    [Fact]
    public async Task RemoveMcp_NoOpForNonExistent()
    {
        var before = await File.ReadAllTextAsync(_configPath, CT);
        await ConfigWriter.RemoveMcpFromConfigAsync(_configPath, "nope", CT);
        var after = await File.ReadAllTextAsync(_configPath, CT);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task RemoveMcp_DoesNotAffectSkillsWithSameName()
    {
        await ConfigWriter.AddSkillToConfigAsync(_configPath, "github", "org/github-skill", ct: CT);
        await ConfigWriter.AddMcpToConfigAsync(_configPath, new McpConfig("github", "npx", null, null, null, []), CT);
        await ConfigWriter.RemoveMcpFromConfigAsync(_configPath, "github", CT);

        var config = await ConfigLoader.LoadAsync(_configPath, CT);
        Assert.Empty(config.Mcp);
        Assert.Contains(config.Skills.OfType<RegularSkillDependency>(), s => s.Name == "github");
    }
}
