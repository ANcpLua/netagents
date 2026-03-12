using NetAgents.Skills;
using Xunit;

namespace NetAgents.Tests.Skills;

public class DiscoverSkillTests : IAsyncLifetime
{
    private string _repoDir = null!;

    public async ValueTask InitializeAsync()
    {
        _repoDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_repoDir);
        await ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (Directory.Exists(_repoDir))
            Directory.Delete(_repoDir, recursive: true);
        await ValueTask.CompletedTask;
    }

    private CancellationToken CT => TestContext.Current.CancellationToken;

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

        Assert.NotNull(result);
        Assert.Equal("pdf", result.Path);
        Assert.Equal("pdf", result.Meta.Name);
    }

    [Fact]
    public async Task FindsSkillInSkillsDirectory()
    {
        await WriteSkillAsync("skills/review", "review");

        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "review", CT);

        Assert.NotNull(result);
        Assert.Equal("skills/review", result.Path);
    }

    [Fact]
    public async Task FindsSkillInAgentsSkillsDirectory()
    {
        await WriteSkillAsync(".agents/skills/lint", "lint");

        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "lint", CT);

        Assert.NotNull(result);
        Assert.Equal(".agents/skills/lint", result.Path);
    }

    [Fact]
    public async Task FindsSkillInClaudeSkillsDirectory()
    {
        await WriteSkillAsync(".claude/skills/commit", "commit");

        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "commit", CT);

        Assert.NotNull(result);
        Assert.Equal(".claude/skills/commit", result.Path);
    }

    [Fact]
    public async Task PrefersRootLevelOverSkillsDirectory()
    {
        await WriteSkillAsync("pdf", "pdf");
        await WriteSkillAsync("skills/pdf", "pdf");

        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "pdf", CT);

        Assert.Equal("pdf", result!.Path);
    }

    [Fact]
    public async Task FindsSkillByFrontmatterNameWhenDirectoryNameDiffers()
    {
        await WriteSkillAsync("skills/chat", "chat-sdk");

        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "chat-sdk", CT);

        Assert.NotNull(result);
        Assert.Equal("skills/chat", result.Path);
        Assert.Equal("chat-sdk", result.Meta.Name);
    }

    [Fact]
    public async Task PrefersDirectoryNameMatchOverFrontmatterNameMatch()
    {
        await WriteSkillAsync("skills/my-skill", "my-skill");
        await WriteSkillAsync("skills/other", "my-skill");

        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "my-skill", CT);

        Assert.Equal("skills/my-skill", result!.Path);
    }

    [Fact]
    public async Task PrefersHigherPriorityScanDirFrontmatterMatch()
    {
        // Root-level: ./chat/SKILL.md with name: "chat-sdk" (frontmatter match)
        await WriteSkillAsync("chat", "chat-sdk");
        // skills/: skills/chat-sdk/SKILL.md (dir name match, but lower priority scan dir)
        await WriteSkillAsync("skills/chat-sdk", "chat-sdk");

        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "chat-sdk", CT);

        Assert.Equal("chat", result!.Path);
    }

    [Fact]
    public async Task FindsSkillNestedInCategorySubdirectory()
    {
        await WriteSkillAsync("skills/.curated/pdf", "pdf");

        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "pdf", CT);

        Assert.NotNull(result);
        Assert.Equal("skills/.curated/pdf", result.Path);
        Assert.Equal("pdf", result.Meta.Name);
    }

    [Fact]
    public async Task FindsSkillNestedMultipleLevelsDeep()
    {
        await WriteSkillAsync("skills/org/team/deploy", "deploy");

        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "deploy", CT);

        Assert.NotNull(result);
        Assert.Equal("skills/org/team/deploy", result.Path);
    }

    [Fact]
    public async Task PrefersDirectMatchOverNestedMatch()
    {
        await WriteSkillAsync("skills/pdf", "pdf");
        await WriteSkillAsync("skills/.curated/pdf", "pdf");

        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "pdf", CT);

        Assert.Equal("skills/pdf", result!.Path);
    }

    [Fact]
    public async Task FindsSkillByFrontmatterNameInNestedCategoryDirectory()
    {
        await WriteSkillAsync("skills/experimental/chat", "chat-sdk");

        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "chat-sdk", CT);

        Assert.NotNull(result);
        Assert.Equal("skills/experimental/chat", result.Path);
        Assert.Equal("chat-sdk", result.Meta.Name);
    }

    [Fact]
    public async Task DoesNotDescendIntoSkillDirectories()
    {
        await WriteSkillAsync("skills/outer", "outer");
        await WriteSkillAsync("skills/outer/nested", "nested");

        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "nested", CT);

        Assert.Null(result);
    }

    [Fact]
    public async Task ReturnsNullWhenSkillNotFound()
    {
        var result = await SkillDiscovery.DiscoverSkillAsync(_repoDir, "nonexistent", CT);

        Assert.Null(result);
    }
}

public class DiscoverAllSkillsTests : IAsyncLifetime
{
    private string _repoDir = null!;

    public async ValueTask InitializeAsync()
    {
        _repoDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_repoDir);
        await ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (Directory.Exists(_repoDir))
            Directory.Delete(_repoDir, recursive: true);
        await ValueTask.CompletedTask;
    }

    private CancellationToken CT => TestContext.Current.CancellationToken;

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

        Assert.Equal(2, results.Count);
        var names = results.Select(r => r.Meta.Name).Order().ToList();
        Assert.Equal(["pdf", "review"], names);
    }

    [Fact]
    public async Task ReturnsEmptyForRepoWithNoSkills()
    {
        var results = await SkillDiscovery.DiscoverAllSkillsAsync(_repoDir, CT);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SkipsDirectoriesWithoutSkillMd()
    {
        var dir = Path.Combine(_repoDir, "not-a-skill");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "README.md"), "# Not a skill", CT);

        var results = await SkillDiscovery.DiscoverAllSkillsAsync(_repoDir, CT);

        Assert.Empty(results);
    }

    [Fact]
    public async Task DiscoversSkillsInCategorySubdirectories()
    {
        await WriteSkillAsync("skills/.curated/pdf", "pdf");
        await WriteSkillAsync("skills/.curated/sentry", "sentry");
        await WriteSkillAsync("skills/review", "review");

        var results = await SkillDiscovery.DiscoverAllSkillsAsync(_repoDir, CT);

        Assert.Equal(3, results.Count);
        var names = results.Select(r => r.Meta.Name).Order().ToList();
        Assert.Equal(["pdf", "review", "sentry"], names);
        Assert.Equal("skills/.curated/pdf", results.First(r => r.Meta.Name == "pdf").Path);
    }

    [Fact]
    public async Task DoesNotDescendIntoSkillDirectories()
    {
        await WriteSkillAsync("skills/outer", "outer");
        await WriteSkillAsync("skills/outer/nested", "nested");

        var results = await SkillDiscovery.DiscoverAllSkillsAsync(_repoDir, CT);

        Assert.Single(results);
        Assert.Equal("outer", results[0].Meta.Name);
    }

    [Fact]
    public async Task DiscoversSkillsInMarketplaceFormat()
    {
        Directory.CreateDirectory(Path.Combine(_repoDir, ".claude-plugin"));
        await WriteSkillAsync("plugins/my-plugin/skills/find-bugs", "find-bugs");
        await WriteSkillAsync("plugins/my-plugin/skills/code-review", "code-review");

        var results = await SkillDiscovery.DiscoverAllSkillsAsync(_repoDir, CT);

        Assert.Equal(2, results.Count);
        var names = results.Select(r => r.Meta.Name).Order().ToList();
        Assert.Equal(["code-review", "find-bugs"], names);
        Assert.Equal("plugins/my-plugin/skills/find-bugs",
            results.First(r => r.Meta.Name == "find-bugs").Path);
    }
}
