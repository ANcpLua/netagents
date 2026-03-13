namespace NetAgents.Tests.Lockfile;

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
        Assert.NotNull(loaded);
        Assert.Equal(1, loaded.Version);
        Assert.Equal("anthropics/skills", loaded.Skills["pdf-processing"].Source);
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
        Assert.NotNull(loaded);
        Assert.Equal("path:../shared/my-skill", loaded.Skills["my-skill"].Source);
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
        Assert.NotNull(loaded);
        var keys = loaded.Skills.Keys.ToList();
        Assert.Equal(["a-skill", "z-skill"], keys);
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
        Assert.EndsWith("\n", content);
        Assert.False(content.EndsWith("\n\n"), "File should not end with double newline");
    }

    [Fact]
    public async Task ReturnsNullForMissingLockfile()
    {
        var result =
            await LockfileLoader.LoadAsync(Path.Combine(_dir, "nope.lock"), TestContext.Current.CancellationToken);
        Assert.Null(result);
    }
}
