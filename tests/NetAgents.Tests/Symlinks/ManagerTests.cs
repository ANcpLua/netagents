namespace NetAgents.Tests.Symlinks;

using AwesomeAssertions;
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

        result.Created.Should().BeTrue();
        result.Migrated.Should().BeEmpty();

        var fi = new FileInfo(Path.Combine(targetDir, "skills"));
        fi.LinkTarget.Should().NotBeNull();
        fi.LinkTarget.Should().Be(Path.Combine("..", ".agents", "skills"));
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
        result.Created.Should().BeTrue();

        var entries = Directory.GetFileSystemEntries(targetDir).Select(Path.GetFileName).ToList();
        entries.Should().Contain("settings.json");
        entries.Should().Contain("skills");
    }

    [Fact]
    public async Task IsIdempotentWhenSymlinkAlreadyCorrect()
    {
        var targetDir = Path.Combine(_dir, ".claude");
        await SymlinkManager.EnsureSkillsSymlinkAsync(_agentsDir, targetDir, TestContext.Current.CancellationToken);
        var result =
            await SymlinkManager.EnsureSkillsSymlinkAsync(_agentsDir, targetDir, TestContext.Current.CancellationToken);
        result.Created.Should().BeFalse();
    }

    [Fact]
    public async Task ReplacesWrongSymlink()
    {
        var targetDir = Path.Combine(_dir, ".claude");
        Directory.CreateDirectory(targetDir);
        File.CreateSymbolicLink(Path.Combine(targetDir, "skills"), "/wrong/target");

        var result =
            await SymlinkManager.EnsureSkillsSymlinkAsync(_agentsDir, targetDir, TestContext.Current.CancellationToken);
        result.Created.Should().BeTrue();

        var fi = new FileInfo(Path.Combine(targetDir, "skills"));
        fi.LinkTarget.Should().Be(Path.Combine("..", ".agents", "skills"));
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
        result.Created.Should().BeTrue();
        result.Migrated.Should().Contain("my-local-skill");

        var agentsEntries = Directory.GetDirectories(Path.Combine(_agentsDir, "skills"))
            .Select(Path.GetFileName).ToList();
        agentsEntries.Should().Contain("my-local-skill");

        var fi = new FileInfo(Path.Combine(targetDir, "skills"));
        fi.LinkTarget.Should().NotBeNull();
    }

    [Fact]
    public async Task RemovesMigratedFilesFromGitIndex()
    {
        var ct = TestContext.Current.CancellationToken;

        await ProcessRunner.RunAsync("git", ["init"], _dir, ct: ct);
        await ProcessRunner.RunAsync("git", ["config", "user.email", "test@test.com"], _dir, ct: ct);
        await ProcessRunner.RunAsync("git", ["config", "user.name", "Test"], _dir, ct: ct);

        var targetDir = Path.Combine(_dir, ".claude");
        var realSkillsDir = Path.Combine(targetDir, "skills");
        Directory.CreateDirectory(Path.Combine(realSkillsDir, "my-skill"));
        await File.WriteAllTextAsync(
            Path.Combine(realSkillsDir, "my-skill", "SKILL.md"),
            "---\nname: test\n---\n", ct);

        await ProcessRunner.RunAsync("git", ["add", "."], _dir, ct: ct);
        await ProcessRunner.RunAsync("git", ["commit", "-m", "initial"], _dir, ct: ct);

        var before = await ProcessRunner.RunAsync("git", ["ls-files", ".claude/skills/"], _dir, ct: ct);
        before.Stdout.Trim().Should().Contain("my-skill/SKILL.md");

        var result = await SymlinkManager.EnsureSkillsSymlinkAsync(_agentsDir, targetDir, ct);
        result.Created.Should().BeTrue();
        result.Migrated.Should().Contain("my-skill");

        var after = await ProcessRunner.RunAsync("git", ["ls-files", ".claude/skills/"], _dir, ct: ct);
        after.Stdout.Trim().Should().Be("");

        var agentsEntries = Directory.GetDirectories(Path.Combine(_agentsDir, "skills"))
            .Select(Path.GetFileName).ToList();
        agentsEntries.Should().Contain("my-skill");
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
        issues.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsMissingSymlink()
    {
        var targetDir = Path.Combine(_dir, ".claude");
        var issues =
            await SymlinkManager.VerifySymlinksAsync(_agentsDir, [targetDir], TestContext.Current.CancellationToken);
        issues.Should().ContainSingle();
        issues[0].Issue.Should().Contain("does not exist");
    }

    [Fact]
    public async Task ReportsNonSymlinkDirectory()
    {
        var targetDir = Path.Combine(_dir, ".claude");
        Directory.CreateDirectory(Path.Combine(targetDir, "skills"));

        var issues =
            await SymlinkManager.VerifySymlinksAsync(_agentsDir, [targetDir], TestContext.Current.CancellationToken);
        issues.Should().ContainSingle();
        issues[0].Issue.Should().Contain("not a symlink");
    }
}
