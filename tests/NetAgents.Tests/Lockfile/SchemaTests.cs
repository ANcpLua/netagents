namespace NetAgents.Tests.Lockfile;

using AwesomeAssertions;
using NetAgents.Lockfile;
using Xunit;

public class SchemaTests
{
    [Fact]
    public async Task ParsesMinimalLockfile()
    {
        const string toml = "version = 1\n";
        var result = await LockfileLoader.LoadAsync(WriteToTemp(toml), TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.Version.Should().Be(1);
        result.Skills.Should().BeEmpty();
    }

    [Fact]
    public async Task ParsesLockfileWithGitSkill()
    {
        const string toml = """
                            version = 1

                            [skills.pdf-processing]
                            source = "anthropics/skills"
                            resolved_url = "https://github.com/anthropics/skills.git"
                            resolved_path = "pdf-processing"
                            resolved_ref = "v1.2.0"
                            """;

        var result = await LockfileLoader.LoadAsync(WriteToTemp(toml), TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.Skills.ContainsKey("pdf-processing").Should().BeTrue();
    }

    [Fact]
    public async Task ParsesLockfileWithLocalSkill()
    {
        const string toml = """
                            version = 1

                            [skills.my-skill]
                            source = "path:../shared/my-skill"
                            """;

        var result = await LockfileLoader.LoadAsync(WriteToTemp(toml), TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.Skills.ContainsKey("my-skill").Should().BeTrue();
    }

    [Fact]
    public async Task ThrowsOnInvalidVersion()
    {
        const string toml = "version = 2\n";
        await Assert.ThrowsAsync<LockfileException>(() =>
            LockfileLoader.LoadAsync(WriteToTemp(toml), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ParsesGitSkillWithoutResolvedRef()
    {
        const string toml = """
                            version = 1

                            [skills.my-skill]
                            source = "org/repo"
                            resolved_url = "https://github.com/org/repo.git"
                            resolved_path = "my-skill"
                            """;

        var result = await LockfileLoader.LoadAsync(WriteToTemp(toml), TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        var skill = result!.Skills["my-skill"].Should().BeOfType<LockedGitSkill>().Which;
        skill.ResolvedRef.Should().BeNull();
    }

    private static string WriteToTemp(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".lock");
        File.WriteAllText(path, content);
        return path;
    }
}
