using NetAgents.Skills;
using Xunit;

namespace NetAgents.Tests.Skills;

public class LoaderTests : IAsyncLifetime
{
    private string _dir = null!;

    public async ValueTask InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_dir);
        await ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
        await ValueTask.CompletedTask;
    }

    private CancellationToken CT => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ParsesValidSkillMdWithFrontmatter()
    {
        var skillMd = Path.Combine(_dir, "SKILL.md");
        await File.WriteAllTextAsync(skillMd, """
            ---
            name: pdf-processing
            description: Extract and process PDF documents
            license: MIT
            ---

            # PDF Processing

            This skill handles PDF files.
            """, CT);

        var meta = await SkillLoader.LoadSkillMdAsync(skillMd, CT);

        Assert.Equal("pdf-processing", meta.Name);
        Assert.Equal("Extract and process PDF documents", meta.Description);
        Assert.Equal("MIT", meta.Extra["license"]);
    }

    [Fact]
    public async Task HandlesQuotedValues()
    {
        var skillMd = Path.Combine(_dir, "SKILL.md");
        await File.WriteAllTextAsync(skillMd, """
            ---
            name: "my-skill"
            description: 'A skill with quoted values'
            ---

            Content.
            """, CT);

        var meta = await SkillLoader.LoadSkillMdAsync(skillMd, CT);

        Assert.Equal("my-skill", meta.Name);
        Assert.Equal("A skill with quoted values", meta.Description);
    }

    [Fact]
    public async Task ThrowsForMissingFile()
    {
        await Assert.ThrowsAsync<SkillLoadException>(
            () => SkillLoader.LoadSkillMdAsync(Path.Combine(_dir, "nope.md"), CT));
    }

    [Fact]
    public async Task ThrowsForMissingFrontmatter()
    {
        var skillMd = Path.Combine(_dir, "SKILL.md");
        await File.WriteAllTextAsync(skillMd, "# No frontmatter here\n", CT);

        await Assert.ThrowsAsync<SkillLoadException>(
            () => SkillLoader.LoadSkillMdAsync(skillMd, CT));
    }

    [Fact]
    public async Task ThrowsForMissingName()
    {
        var skillMd = Path.Combine(_dir, "SKILL.md");
        await File.WriteAllTextAsync(skillMd, "---\ndescription: No name field\n---\n", CT);

        await Assert.ThrowsAsync<SkillLoadException>(
            () => SkillLoader.LoadSkillMdAsync(skillMd, CT));
    }

    [Fact]
    public async Task ThrowsForMissingDescription()
    {
        var skillMd = Path.Combine(_dir, "SKILL.md");
        await File.WriteAllTextAsync(skillMd, "---\nname: my-skill\n---\n", CT);

        await Assert.ThrowsAsync<SkillLoadException>(
            () => SkillLoader.LoadSkillMdAsync(skillMd, CT));
    }
}
