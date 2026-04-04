namespace NetAgents.Tests.Config;

using AwesomeAssertions;
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

        config.Version.Should().Be(1);
        var pdf = config.Skills.OfType<RegularSkillDependency>().FirstOrDefault(s => s.Name == "pdf");
        pdf.Should().NotBeNull();
        pdf!.Source.Should().Be("anthropics/skills");
        pdf.Ref.Should().Be("v1.0.0");
    }

    [Fact]
    public async Task LoadsMinimalConfig()
    {
        await File.WriteAllTextAsync(ConfigPath, "version = 1\n", CT);

        var config = await ConfigLoader.LoadAsync(ConfigPath, CT);

        config.Version.Should().Be(1);
        config.Skills.Should().BeEmpty();
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

        config.Symlinks.Should().NotBeNull();
        config.Symlinks!.Targets.Should().BeEquivalentTo([".claude"]);
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

        config.Agents.Should().BeEquivalentTo(["claude", "cursor"]);
        config.Mcp.Should().ContainSingle();
        config.Mcp[0].Name.Should().Be("github");
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

        config.Skills.Should().ContainSingle();
        var dep = config.Skills[0].Should().BeOfType<WildcardSkillDependency>().Which;
        dep.Source.Should().Be("getsentry/skills");
    }

    [Fact]
    public async Task LoadsWildcardWithExcludeList()
    {
        await File.WriteAllTextAsync(ConfigPath,
            "version = 1\n\n[[skills]]\nname = \"*\"\nsource = \"getsentry/skills\"\nexclude = [\"deprecated\"]\n", CT);

        var config = await ConfigLoader.LoadAsync(ConfigPath, CT);

        var dep = config.Skills[0].Should().BeOfType<WildcardSkillDependency>().Which;
        dep.Exclude.Should().BeEquivalentTo(["deprecated"]);
    }

    [Fact]
    public async Task DefaultsExcludeToEmptyArray()
    {
        await File.WriteAllTextAsync(ConfigPath,
            "version = 1\n\n[[skills]]\nname = \"*\"\nsource = \"getsentry/skills\"\n", CT);

        var config = await ConfigLoader.LoadAsync(ConfigPath, CT);

        var dep = config.Skills[0].Should().BeOfType<WildcardSkillDependency>().Which;
        dep.Exclude.Should().BeEmpty();
    }

    [Fact]
    public async Task RejectsDuplicateWildcardSources()
    {
        await File.WriteAllTextAsync(ConfigPath,
            "version = 1\n\n[[skills]]\nname = \"*\"\nsource = \"getsentry/skills\"\n\n[[skills]]\nname = \"*\"\nsource = \"getsentry/skills\"\n",
            CT);

        var ex = await Assert.ThrowsAsync<ConfigException>(() => ConfigLoader.LoadAsync(ConfigPath, CT));
        ex.Message.Should().Contain("Duplicate wildcard");
    }

    [Fact]
    public async Task AllowsWildcardsFromDifferentSources()
    {
        await File.WriteAllTextAsync(ConfigPath,
            "version = 1\n\n[[skills]]\nname = \"*\"\nsource = \"getsentry/skills\"\n\n[[skills]]\nname = \"*\"\nsource = \"anthropics/skills\"\n",
            CT);

        var config = await ConfigLoader.LoadAsync(ConfigPath, CT);

        config.Skills.Count.Should().Be(2);
    }

    [Fact]
    public async Task AllowsMixingWildcardAndRegularEntries()
    {
        await File.WriteAllTextAsync(ConfigPath,
            "version = 1\n\n[[skills]]\nname = \"*\"\nsource = \"getsentry/skills\"\n\n[[skills]]\nname = \"pdf\"\nsource = \"anthropics/skills\"\n",
            CT);

        var config = await ConfigLoader.LoadAsync(ConfigPath, CT);

        config.Skills.Count.Should().Be(2);
    }
}
