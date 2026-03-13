namespace NetAgents.Tests.Config;

using NetAgents.Config;
using Xunit;

public class LoaderTests : IAsyncLifetime
{
    private string _dir = null!;

    private string ConfigPath => Path.Combine(_dir, "agents.toml");
    private CancellationToken CT => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_dir);
        await ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
        await ValueTask.CompletedTask;
    }

    [Fact]
    public async Task LoadsValidConfig()
    {
        await File.WriteAllTextAsync(ConfigPath, """
                                                 version = 1

                                                 [[skills]]
                                                 name = "pdf"
                                                 source = "anthropics/skills"
                                                 ref = "v1.0.0"
                                                 """, CT);

        var config = await ConfigLoader.LoadAsync(ConfigPath, CT);

        Assert.Equal(1, config.Version);
        var pdf = config.Skills.OfType<RegularSkillDependency>().FirstOrDefault(s => s.Name == "pdf");
        Assert.NotNull(pdf);
        Assert.Equal("anthropics/skills", pdf.Source);
        Assert.Equal("v1.0.0", pdf.Ref);
    }

    [Fact]
    public async Task LoadsMinimalConfig()
    {
        await File.WriteAllTextAsync(ConfigPath, "version = 1\n", CT);

        var config = await ConfigLoader.LoadAsync(ConfigPath, CT);

        Assert.Equal(1, config.Version);
        Assert.Empty(config.Skills);
    }

    [Fact]
    public async Task ThrowsConfigExceptionForMissingFile()
    {
        await Assert.ThrowsAsync<ConfigException>(() => ConfigLoader.LoadAsync(Path.Combine(_dir, "nope.toml"), CT));
    }

    [Fact]
    public async Task ThrowsConfigExceptionForInvalidToml()
    {
        await File.WriteAllTextAsync(ConfigPath, "this is not valid toml {{{}}", CT);

        await Assert.ThrowsAsync<ConfigException>(() => ConfigLoader.LoadAsync(ConfigPath, CT));
    }

    [Fact]
    public async Task ThrowsConfigExceptionForWrongSchema()
    {
        await File.WriteAllTextAsync(ConfigPath, "version = 99\nfoo = \"bar\"\n", CT);

        await Assert.ThrowsAsync<ConfigException>(() => ConfigLoader.LoadAsync(ConfigPath, CT));
    }

    [Fact]
    public async Task ParsesSymlinksConfig()
    {
        await File.WriteAllTextAsync(ConfigPath, """
                                                 version = 1

                                                 [symlinks]
                                                 targets = [".claude"]
                                                 """, CT);

        var config = await ConfigLoader.LoadAsync(ConfigPath, CT);

        Assert.NotNull(config.Symlinks);
        Assert.Equal([".claude"], config.Symlinks.Targets);
    }

    [Fact]
    public async Task LoadsConfigWithAgentsAndMcp()
    {
        await File.WriteAllTextAsync(ConfigPath, """
                                                 version = 1
                                                 agents = ["claude", "cursor"]

                                                 [[mcp]]
                                                 name = "github"
                                                 command = "npx"
                                                 args = ["-y", "@mcp/server-github"]
                                                 env = ["GITHUB_TOKEN"]
                                                 """, CT);

        var config = await ConfigLoader.LoadAsync(ConfigPath, CT);

        Assert.Equal(["claude", "cursor"], config.Agents);
        Assert.Single(config.Mcp);
        Assert.Equal("github", config.Mcp[0].Name);
    }

    [Fact]
    public async Task RejectsUnknownAgentIds()
    {
        await File.WriteAllTextAsync(ConfigPath, "version = 1\nagents = [\"claude\", \"emacs\"]\n", CT);

        var ex = await Assert.ThrowsAsync<ConfigException>(() => ConfigLoader.LoadAsync(ConfigPath, CT));
        Assert.Matches("Unknown agent.*emacs", ex.Message);
    }

    [Fact]
    public async Task LoadsWildcardSkillEntry()
    {
        await File.WriteAllTextAsync(ConfigPath,
            "version = 1\n\n[[skills]]\nname = \"*\"\nsource = \"getsentry/skills\"\n", CT);

        var config = await ConfigLoader.LoadAsync(ConfigPath, CT);

        Assert.Single(config.Skills);
        var dep = Assert.IsType<WildcardSkillDependency>(config.Skills[0]);
        Assert.Equal("getsentry/skills", dep.Source);
    }

    [Fact]
    public async Task LoadsWildcardWithExcludeList()
    {
        await File.WriteAllTextAsync(ConfigPath,
            "version = 1\n\n[[skills]]\nname = \"*\"\nsource = \"getsentry/skills\"\nexclude = [\"deprecated\"]\n", CT);

        var config = await ConfigLoader.LoadAsync(ConfigPath, CT);

        var dep = Assert.IsType<WildcardSkillDependency>(config.Skills[0]);
        Assert.Equal(["deprecated"], dep.Exclude);
    }

    [Fact]
    public async Task DefaultsExcludeToEmptyArray()
    {
        await File.WriteAllTextAsync(ConfigPath,
            "version = 1\n\n[[skills]]\nname = \"*\"\nsource = \"getsentry/skills\"\n", CT);

        var config = await ConfigLoader.LoadAsync(ConfigPath, CT);

        var dep = Assert.IsType<WildcardSkillDependency>(config.Skills[0]);
        Assert.Empty(dep.Exclude);
    }

    [Fact]
    public async Task RejectsDuplicateWildcardSources()
    {
        await File.WriteAllTextAsync(ConfigPath,
            "version = 1\n\n[[skills]]\nname = \"*\"\nsource = \"getsentry/skills\"\n\n[[skills]]\nname = \"*\"\nsource = \"getsentry/skills\"\n",
            CT);

        var ex = await Assert.ThrowsAsync<ConfigException>(() => ConfigLoader.LoadAsync(ConfigPath, CT));
        Assert.Contains("Duplicate wildcard", ex.Message);
    }

    [Fact]
    public async Task AllowsWildcardsFromDifferentSources()
    {
        await File.WriteAllTextAsync(ConfigPath,
            "version = 1\n\n[[skills]]\nname = \"*\"\nsource = \"getsentry/skills\"\n\n[[skills]]\nname = \"*\"\nsource = \"anthropics/skills\"\n",
            CT);

        var config = await ConfigLoader.LoadAsync(ConfigPath, CT);

        Assert.Equal(2, config.Skills.Count);
    }

    [Fact]
    public async Task AllowsMixingWildcardAndRegularEntries()
    {
        await File.WriteAllTextAsync(ConfigPath,
            "version = 1\n\n[[skills]]\nname = \"*\"\nsource = \"getsentry/skills\"\n\n[[skills]]\nname = \"pdf\"\nsource = \"anthropics/skills\"\n",
            CT);

        var config = await ConfigLoader.LoadAsync(ConfigPath, CT);

        Assert.Equal(2, config.Skills.Count);
    }
}
