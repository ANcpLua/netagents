namespace NetAgents.Tests.Skills;

using AwesomeAssertions;
using NetAgents.Skills;
using Xunit;

public class DiscoverSkillTests : IAsyncLifetime
{
    private string _repoDir = null!;
    private CancellationToken CT => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _repoDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_repoDir);
        await ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (Directory.Exists(_repoDir)) Directory.Delete(_repoDir, true);
        await ValueTask.CompletedTask;
    }

    private static string SkillMd(string name) =>
        $"---\nname: {name}\ndescription: Test skill {name}\n---\n\n# {name}\n";

    private async Task WriteSkillAsync(string relativePath, string name)
    {
        var dir = Path.Combine(_repoDir, relativePath);
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "SKILL.md"), SkillMd(name), CT);
    }

    [Fact]
    public async Task FindsSkillAtRootLevel()
    {
        await WriteSkillAsync("pdf", "pdf");
        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "pdf", CT);
        result.Should().NotBeNull();
        result!.Path.Should().Be("pdf");
        result.Meta.Name.Should().Be("pdf");
    }

    [Fact]
    public async Task FindsSkillInSkillsDirectory()
    {
        await WriteSkillAsync("skills/review", "review");
        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "review", CT);
        result.Should().NotBeNull();
        result!.Path.Should().Be("skills/review");
    }

    [Fact]
    public async Task FindsSkillInAgentsSkillsDirectory()
    {
        await WriteSkillAsync(".agents/skills/lint", "lint");
        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "lint", CT);
        result.Should().NotBeNull();
        result!.Path.Should().Be(".agents/skills/lint");
    }

    [Fact]
    public async Task FindsSkillInClaudeSkillsDirectory()
    {
        await WriteSkillAsync(".claude/skills/commit", "commit");
        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "commit", CT);
        result.Should().NotBeNull();
        result!.Path.Should().Be(".claude/skills/commit");
    }

    [Fact]
    public async Task PrefersRootLevelOverSkillsDirectory()
    {
        await WriteSkillAsync("pdf", "pdf");
        await WriteSkillAsync("skills/pdf", "pdf");
        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "pdf", CT);
        result!.Path.Should().Be("pdf");
    }

    [Fact]
    public async Task FindsSkillByFrontmatterNameWhenDirectoryNameDiffers()
    {
        await WriteSkillAsync("skills/chat", "chat-sdk");
        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "chat-sdk", CT);
        result.Should().NotBeNull();
        result!.Path.Should().Be("skills/chat");
        result.Meta.Name.Should().Be("chat-sdk");
    }

    [Fact]
    public async Task PrefersDirectoryNameMatchOverFrontmatterNameMatch()
    {
        await WriteSkillAsync("skills/my-skill", "my-skill");
        await WriteSkillAsync("skills/other", "my-skill");
        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "my-skill", CT);
        result!.Path.Should().Be("skills/my-skill");
    }

    [Fact]
    public async Task PrefersHigherPriorityScanDirFrontmatterMatch()
    {
        await WriteSkillAsync("chat", "chat-sdk");
        await WriteSkillAsync("skills/chat-sdk", "chat-sdk");
        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "chat-sdk", CT);
        result!.Path.Should().Be("chat");
    }

    [Fact]
    public async Task FindsSkillNestedInCategorySubdirectory()
    {
        await WriteSkillAsync("skills/.curated/pdf", "pdf");
        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "pdf", CT);
        result.Should().NotBeNull();
        result!.Path.Should().Be("skills/.curated/pdf");
        result.Meta.Name.Should().Be("pdf");
    }

    [Fact]
    public async Task FindsSkillNestedMultipleLevelsDeep()
    {
        await WriteSkillAsync("skills/org/team/deploy", "deploy");
        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "deploy", CT);
        result.Should().NotBeNull();
        result!.Path.Should().Be("skills/org/team/deploy");
    }

    [Fact]
    public async Task PrefersDirectMatchOverNestedMatch()
    {
        await WriteSkillAsync("skills/pdf", "pdf");
        await WriteSkillAsync("skills/.curated/pdf", "pdf");
        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "pdf", CT);
        result!.Path.Should().Be("skills/pdf");
    }

    [Fact]
    public async Task FindsSkillByFrontmatterNameInNestedCategoryDirectory()
    {
        await WriteSkillAsync("skills/experimental/chat", "chat-sdk");
        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "chat-sdk", CT);
        result.Should().NotBeNull();
        result!.Path.Should().Be("skills/experimental/chat");
        result.Meta.Name.Should().Be("chat-sdk");
    }

    [Fact]
    public async Task DoesNotDescendIntoSkillDirectories()
    {
        await WriteSkillAsync("skills/outer", "outer");
        await WriteSkillAsync("skills/outer/nested", "nested");
        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "nested", CT);
        result.Should().BeNull();
    }

    [Fact]
    public async Task ReturnsNullWhenSkillNotFound()
    {
        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "nonexistent", CT);
        result.Should().BeNull();
    }
}

public class DiscoverAllSkillsTests : IAsyncLifetime
{
    private string _repoDir = null!;
    private CancellationToken CT => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _repoDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_repoDir);
        await ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (Directory.Exists(_repoDir)) Directory.Delete(_repoDir, true);
        await ValueTask.CompletedTask;
    }

    private static string SkillMd(string name) =>
        $"---\nname: {name}\ndescription: Test skill {name}\n---\n\n# {name}\n";

    private async Task WriteSkillAsync(string relativePath, string name)
    {
        var dir = Path.Combine(_repoDir, relativePath);
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "SKILL.md"), SkillMd(name), CT);
    }

    [Fact]
    public async Task DiscoversSkillsAcrossMultipleDirectories()
    {
        await WriteSkillAsync("pdf", "pdf");
        await WriteSkillAsync("skills/review", "review");
        var results = await SkillDiscovery.DiscoverAllSkillsAsync(_repoDir, CT);
        results.Count.Should().Be(2);
        var names = results.Select(r => r.Meta.Name).Order().ToList();
        names.Should().BeEquivalentTo(["pdf", "review"]);
    }

    [Fact]
    public async Task ReturnsEmptyForRepoWithNoSkills()
    {
        var results = await SkillDiscovery.DiscoverAllSkillsAsync(_repoDir, CT);
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SkipsDirectoriesWithoutSkillMd()
    {
        var dir = Path.Combine(_repoDir, "not-a-skill");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "README.md"), "# Not a skill", CT);
        var results = await SkillDiscovery.DiscoverAllSkillsAsync(_repoDir, CT);
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoversSkillsInCategorySubdirectories()
    {
        await WriteSkillAsync("skills/.curated/pdf", "pdf");
        await WriteSkillAsync("skills/.curated/sentry", "sentry");
        await WriteSkillAsync("skills/review", "review");
        var results = await SkillDiscovery.DiscoverAllSkillsAsync(_repoDir, CT);
        results.Count.Should().Be(3);
        var names = results.Select(r => r.Meta.Name).Order().ToList();
        names.Should().BeEquivalentTo(["pdf", "review", "sentry"]);
        results.First(r => r.Meta.Name == "pdf").Path.Should().Be("skills/.curated/pdf");
    }

    [Fact]
    public async Task DoesNotDescendIntoSkillDirectories()
    {
        await WriteSkillAsync("skills/outer", "outer");
        await WriteSkillAsync("skills/outer/nested", "nested");
        var results = await SkillDiscovery.DiscoverAllSkillsAsync(_repoDir, CT);
        results.Should().ContainSingle();
        results[0].Meta.Name.Should().Be("outer");
    }

    [Fact]
    public async Task DiscoversSkillsInMarketplaceFormat()
    {
        Directory.CreateDirectory(Path.Combine(_repoDir, ".claude-plugin"));
        await WriteSkillAsync("plugins/my-plugin/skills/find-bugs", "find-bugs");
        await WriteSkillAsync("plugins/my-plugin/skills/code-review", "code-review");
        var results = await SkillDiscovery.DiscoverAllSkillsAsync(_repoDir, CT);
        results.Count.Should().Be(2);
        var names = results.Select(r => r.Meta.Name).Order().ToList();
        names.Should().BeEquivalentTo(["code-review", "find-bugs"]);
        results.First(r => r.Meta.Name == "find-bugs").Path.Should().Be("plugins/my-plugin/skills/find-bugs");
    }
}
