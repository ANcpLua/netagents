namespace NetAgents.Tests.Lockfile;

using AwesomeAssertions;
using NetAgents.Lockfile;
using Xunit;

public class WriterTests : IAsyncLifetime
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
            Directory.Delete(_dir, true);
        await ValueTask.CompletedTask;
    }

    [Fact]
    public async Task RoundTripsLockfileWithGitSkill()
    {
        var lockPath = Path.Combine(_dir, "agents.lock");
        var data = new LockfileData(1, new Dictionary<string, LockedSkill>
        {
            ["pdf-processing"] = new LockedGitSkill(
                "anthropics/skills",
                "https://github.com/anthropics/skills.git",
                "pdf-processing",
                "v1.2.0")
        });

        await LockfileWriter.WriteAsync(lockPath, data, TestContext.Current.CancellationToken);

        var loaded = await LockfileLoader.LoadAsync(lockPath, TestContext.Current.CancellationToken);
        loaded.Should().NotBeNull();
        loaded!.Version.Should().Be(1);
        loaded.Skills["pdf-processing"].Source.Should().Be("anthropics/skills");
    }

    [Fact]
    public async Task RoundTripsLockfileWithLocalSkill()
    {
        var lockPath = Path.Combine(_dir, "agents.lock");
        var data = new LockfileData(1, new Dictionary<string, LockedSkill>
        {
            ["my-skill"] = new LockedLocalSkill("path:../shared/my-skill")
        });

        await LockfileWriter.WriteAsync(lockPath, data, TestContext.Current.CancellationToken);

        var loaded = await LockfileLoader.LoadAsync(lockPath, TestContext.Current.CancellationToken);
        loaded.Should().NotBeNull();
        loaded!.Skills["my-skill"].Source.Should().Be("path:../shared/my-skill");
    }

    [Fact]
    public async Task SortsSkillsAlphabetically()
    {
        var lockPath = Path.Combine(_dir, "agents.lock");
        var data = new LockfileData(1, new Dictionary<string, LockedSkill>
        {
            ["z-skill"] = new LockedLocalSkill("org/z-repo"),
            ["a-skill"] = new LockedLocalSkill("org/a-repo")
        });

        await LockfileWriter.WriteAsync(lockPath, data, TestContext.Current.CancellationToken);

        var loaded = await LockfileLoader.LoadAsync(lockPath, TestContext.Current.CancellationToken);
        loaded.Should().NotBeNull();
        var keys = loaded!.Skills.Keys.ToList();
        keys.Should().BeEquivalentTo(["a-skill", "z-skill"]);
    }

    [Fact]
    public async Task EndsWithExactlyOneTrailingNewline()
    {
        var lockPath = Path.Combine(_dir, "agents.lock");
        var data = new LockfileData(1, new Dictionary<string, LockedSkill>
        {
            ["test-skill"] = new LockedLocalSkill("org/repo")
        });

        await LockfileWriter.WriteAsync(lockPath, data, TestContext.Current.CancellationToken);

        var content = await File.ReadAllTextAsync(lockPath, TestContext.Current.CancellationToken);
        content.Should().EndWith("\n");
        content.EndsWith("\n\n").Should().BeFalse("File should not end with double newline");
    }

    [Fact]
    public async Task ReturnsNullForMissingLockfile()
    {
        var result =
            await LockfileLoader.LoadAsync(Path.Combine(_dir, "nope.lock"), TestContext.Current.CancellationToken);
        result.Should().BeNull();
    }
}
