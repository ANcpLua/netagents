namespace NetAgents.Tests.Symlinks;

using NetAgents.Tests;
using NetAgents.Symlinks;
using Utils;
using Xunit;

public class EnsureSkillsSymlinkTests : IAsyncLifetime
{
    private string _agentsDir = null!;
    private string _dir = null!;

    public async ValueTask InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _agentsDir = Path.Combine(_dir, ".agents");
        Directory.CreateDirectory(Path.Combine(_agentsDir, "skills"));
        await ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        TestWorkspace.DeleteDirectory(_dir);
        await ValueTask.CompletedTask;
    }

    [Fact]
    public async Task CreatesSymlinkWhenTargetDirDoesNotExist()
    {
        var targetDir = Path.Combine(_dir, ".claude");
        var result =
            await SymlinkManager.EnsureSkillsSymlinkAsync(_agentsDir, targetDir, TestContext.Current.CancellationToken);

        Assert.True(result.Created);
        Assert.Empty(result.Migrated);

        var fi = new FileInfo(Path.Combine(targetDir, "skills"));
        Assert.NotNull(fi.LinkTarget);

        Assert.Equal(Path.Combine("..", ".agents", "skills"), fi.LinkTarget);
    }

    [Fact]
    public async Task CreatesSymlinkWhenTargetDirExistsButSkillsDoesNot()
    {
        var targetDir = Path.Combine(_dir, ".claude");
        Directory.CreateDirectory(targetDir);
        await File.WriteAllTextAsync(Path.Combine(targetDir, "settings.json"), "{}",
            TestContext.Current.CancellationToken);

        var result =
            await SymlinkManager.EnsureSkillsSymlinkAsync(_agentsDir, targetDir, TestContext.Current.CancellationToken);
        Assert.True(result.Created);

        var entries = Directory.GetFileSystemEntries(targetDir).Select(Path.GetFileName).ToList();
        Assert.Contains("settings.json", entries);
        Assert.Contains("skills", entries);
    }

    [Fact]
    public async Task IsIdempotentWhenSymlinkAlreadyCorrect()
    {
        var targetDir = Path.Combine(_dir, ".claude");
        await SymlinkManager.EnsureSkillsSymlinkAsync(_agentsDir, targetDir, TestContext.Current.CancellationToken);
        var result =
            await SymlinkManager.EnsureSkillsSymlinkAsync(_agentsDir, targetDir, TestContext.Current.CancellationToken);
        Assert.False(result.Created);
    }

    [Fact]
    public async Task ReplacesWrongSymlink()
    {
        var targetDir = Path.Combine(_dir, ".claude");
        Directory.CreateDirectory(targetDir);

        // Create a wrong symlink
        File.CreateSymbolicLink(Path.Combine(targetDir, "skills"), "/wrong/target");

        var result =
            await SymlinkManager.EnsureSkillsSymlinkAsync(_agentsDir, targetDir, TestContext.Current.CancellationToken);
        Assert.True(result.Created);

        var fi = new FileInfo(Path.Combine(targetDir, "skills"));
        Assert.Equal(Path.Combine("..", ".agents", "skills"), fi.LinkTarget);
    }

    [Fact]
    public async Task MigratesExistingRealDirectory()
    {
        var targetDir = Path.Combine(_dir, ".claude");
        var realSkillsDir = Path.Combine(targetDir, "skills");
        Directory.CreateDirectory(Path.Combine(realSkillsDir, "my-local-skill"));
        await File.WriteAllTextAsync(
            Path.Combine(realSkillsDir, "my-local-skill", "SKILL.md"),
            "---\nname: test\n---\n",
            TestContext.Current.CancellationToken);

        var result =
            await SymlinkManager.EnsureSkillsSymlinkAsync(_agentsDir, targetDir, TestContext.Current.CancellationToken);
        Assert.True(result.Created);
        Assert.Contains("my-local-skill", result.Migrated);

        // Verify the skill was moved to .agents/skills/
        var agentsEntries = Directory.GetDirectories(Path.Combine(_agentsDir, "skills"))
            .Select(Path.GetFileName).ToList();
        Assert.Contains("my-local-skill", agentsEntries);

        // Verify symlink is now in place
        var fi = new FileInfo(Path.Combine(targetDir, "skills"));
        Assert.NotNull(fi.LinkTarget);
    }

    [Fact]
    public async Task RemovesMigratedFilesFromGitIndex()
    {
        var ct = TestContext.Current.CancellationToken;

        // Initialize a git repo in the temp dir
        await ProcessRunner.RunAsync("git", ["init"], _dir, ct: ct);
        await ProcessRunner.RunAsync("git", ["config", "user.email", "test@test.com"], _dir, ct: ct);
        await ProcessRunner.RunAsync("git", ["config", "user.name", "Test"], _dir, ct: ct);

        // Create a real skills directory with a committed file
        var targetDir = Path.Combine(_dir, ".claude");
        var realSkillsDir = Path.Combine(targetDir, "skills");
        Directory.CreateDirectory(Path.Combine(realSkillsDir, "my-skill"));
        await File.WriteAllTextAsync(
            Path.Combine(realSkillsDir, "my-skill", "SKILL.md"),
            "---\nname: test\n---\n",
            ct);

        await ProcessRunner.RunAsync("git", ["add", "."], _dir, ct: ct);
        await ProcessRunner.RunAsync("git", ["commit", "-m", "initial"], _dir, ct: ct);

        // Verify file is tracked before migration
        var before = await ProcessRunner.RunAsync("git", ["ls-files", ".claude/skills/"], _dir, ct: ct);
        Assert.Contains("my-skill/SKILL.md", before.Stdout.Trim());

        // Run the symlink migration
        var result = await SymlinkManager.EnsureSkillsSymlinkAsync(_agentsDir, targetDir, ct);
        Assert.True(result.Created);
        Assert.Contains("my-skill", result.Migrated);

        // Verify file is no longer in git index
        var after = await ProcessRunner.RunAsync("git", ["ls-files", ".claude/skills/"], _dir, ct: ct);
        Assert.Equal("", after.Stdout.Trim());

        // Verify the skill was moved to .agents/skills/
        var agentsEntries = Directory.GetDirectories(Path.Combine(_agentsDir, "skills"))
            .Select(Path.GetFileName).ToList();
        Assert.Contains("my-skill", agentsEntries);
    }
}

public class VerifySymlinksTests : IAsyncLifetime
{
    private string _agentsDir = null!;
    private string _dir = null!;

    public async ValueTask InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _agentsDir = Path.Combine(_dir, ".agents");
        Directory.CreateDirectory(Path.Combine(_agentsDir, "skills"));
        await ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        TestWorkspace.DeleteDirectory(_dir);
        await ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ReturnsNoIssuesWhenAllSymlinksCorrect()
    {
        var targetDir = Path.Combine(_dir, ".claude");
        await SymlinkManager.EnsureSkillsSymlinkAsync(_agentsDir, targetDir, TestContext.Current.CancellationToken);

        var issues =
            await SymlinkManager.VerifySymlinksAsync(_agentsDir, [targetDir], TestContext.Current.CancellationToken);
        Assert.Empty(issues);
    }

    [Fact]
    public async Task ReportsMissingSymlink()
    {
        var targetDir = Path.Combine(_dir, ".claude");
        var issues =
            await SymlinkManager.VerifySymlinksAsync(_agentsDir, [targetDir], TestContext.Current.CancellationToken);
        Assert.Single(issues);
        Assert.Contains("does not exist", issues[0].Issue);
    }

    [Fact]
    public async Task ReportsNonSymlinkDirectory()
    {
        var targetDir = Path.Combine(_dir, ".claude");
        Directory.CreateDirectory(Path.Combine(targetDir, "skills"));

        var issues =
            await SymlinkManager.VerifySymlinksAsync(_agentsDir, [targetDir], TestContext.Current.CancellationToken);
        Assert.Single(issues);
        Assert.Contains("not a symlink", issues[0].Issue);
    }
}
