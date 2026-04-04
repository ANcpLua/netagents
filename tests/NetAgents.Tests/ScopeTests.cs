namespace NetAgents.Tests;

using AwesomeAssertions;
using Xunit;

/// Shared temp-dir lifetime for test classes that need a disposable directory.
file sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Directory.CreateDirectory(Path);
    }

    public string Path { get; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(Path)) Directory.Delete(Path, true);
    }

    public string Sub(params string[] parts)
    {
        return System.IO.Path.Combine([Path, .. parts]);
    }
}

// ── resolveScope ──────────────────────────────────────────────────────────────

public sealed class ResolveScopeTests : IDisposable
{
    private readonly string? _savedHome = Environment.GetEnvironmentVariable("NETAGENTS_HOME");

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("NETAGENTS_HOME", _savedHome);
    }

    [Fact]
    public void ProjectScope_UsesProjectRoot()
    {
        var s = ScopeResolver.ResolveScope(ScopeKind.Project, "/tmp/my-project");

        s.Scope.Should().Be(ScopeKind.Project);
        s.Root.Should().Be("/tmp/my-project");
        s.AgentsDir.Should().Be(Path.Combine("/tmp/my-project", ".agents"));
        s.ConfigPath.Should().Be(Path.Combine("/tmp/my-project", "agents.toml"));
        s.LockPath.Should().Be(Path.Combine("/tmp/my-project", "agents.lock"));
        s.SkillsDir.Should().Be(Path.Combine("/tmp/my-project", ".agents", "skills"));
    }

    [Fact]
    public void UserScope_UsesHomeDirByDefault()
    {
        Environment.SetEnvironmentVariable("NETAGENTS_HOME", null);
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agents");

        var s = ScopeResolver.ResolveScope(ScopeKind.User);

        s.Scope.Should().Be(ScopeKind.User);
        s.Root.Should().Be(expected);
        s.AgentsDir.Should().Be(expected);
        s.ConfigPath.Should().Be(Path.Combine(expected, "agents.toml"));
        s.LockPath.Should().Be(Path.Combine(expected, "agents.lock"));
        s.SkillsDir.Should().Be(Path.Combine(expected, "skills"));
    }

    [Fact]
    public void UserScope_RespectsNetagentsHomeOverride()
    {
        Environment.SetEnvironmentVariable("NETAGENTS_HOME", "/tmp/fake-home");
        var s = ScopeResolver.ResolveScope(ScopeKind.User);

        s.Root.Should().Be("/tmp/fake-home");
        s.AgentsDir.Should().Be("/tmp/fake-home");
        s.SkillsDir.Should().Be(Path.Combine("/tmp/fake-home", "skills"));
    }

    [Fact]
    public void UserScope_AgentsDirEqualsRoot()
    {
        Environment.SetEnvironmentVariable("NETAGENTS_HOME", "/tmp/user-agents");
        var s = ScopeResolver.ResolveScope(ScopeKind.User);

        s.AgentsDir.Should().Be(s.Root);
    }

    [Fact]
    public void ProjectScope_DefaultsToCwdWhenNoProjectRootGiven()
    {
        var s = ScopeResolver.ResolveScope(ScopeKind.Project);

        s.Root.Should().Be(Directory.GetCurrentDirectory());
    }
}

// ── isInsideGitRepo ───────────────────────────────────────────────────────────

public sealed class IsInsideGitRepoTests
{
    [Fact]
    public void ReturnsTrueWhenDotGitExistsInDir()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(tmp.Sub(".git"));

        ScopeResolver.IsInsideGitRepo(tmp.Path).Should().BeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenDotGitExistsInParent()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(tmp.Sub(".git"));
        var child = tmp.Sub("sub", "deep");
        Directory.CreateDirectory(child);

        ScopeResolver.IsInsideGitRepo(child).Should().BeTrue();
    }

    [Fact]
    public void ReturnsFalseWhenNoDotGitInAnyParent()
    {
        using var tmp = new TempDir();

        ScopeResolver.IsInsideGitRepo(tmp.Path).Should().BeFalse();
    }
}

// ── findGitDir ────────────────────────────────────────────────────────────────

public sealed class FindGitDirTests
{
    [Fact]
    public void ReturnsDotGitDirectoryPath()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(tmp.Sub(".git"));

        ScopeResolver.FindGitDir(tmp.Path).Should().Be(tmp.Sub(".git"));
    }

    [Fact]
    public void ResolvesDotGitFile_SubmoduleToRealGitDir()
    {
        using var tmp = new TempDir();
        var realGitDir = tmp.Sub("real-git-dir");
        Directory.CreateDirectory(realGitDir);
        File.WriteAllText(tmp.Sub(".git"), $"gitdir: {realGitDir}\n");

        ScopeResolver.FindGitDir(tmp.Path).Should().Be(realGitDir);
    }

    [Fact]
    public void ResolvesWorktreeDotGitFile_ToCommonGitDir()
    {
        using var tmp = new TempDir();
        var mainGitDir = tmp.Sub("main-repo", ".git");
        var worktreeGitDir = tmp.Sub("main-repo", ".git", "worktrees", "my-wt");
        Directory.CreateDirectory(worktreeGitDir);
        File.WriteAllText(Path.Combine(worktreeGitDir, "commondir"), "../..\n");

        var worktreeDir = tmp.Sub("my-worktree");
        Directory.CreateDirectory(worktreeDir);
        File.WriteAllText(Path.Combine(worktreeDir, ".git"), $"gitdir: {worktreeGitDir}\n");

        ScopeResolver.FindGitDir(worktreeDir).Should().Be(mainGitDir);
    }

    [Fact]
    public void ReturnsNull_ForDotGitFileWithInvalidGitdirTarget()
    {
        using var tmp = new TempDir();
        File.WriteAllText(tmp.Sub(".git"), "gitdir: /nonexistent/path\n");

        ScopeResolver.FindGitDir(tmp.Path).Should().BeNull();
    }

    [Fact]
    public void ReturnsNull_WhenNoDotGitExists()
    {
        using var tmp = new TempDir();

        ScopeResolver.FindGitDir(tmp.Path).Should().BeNull();
    }
}

// ── resolveDefaultScope ───────────────────────────────────────────────────────

public sealed class ResolveDefaultScopeTests : IDisposable
{
    private readonly string? _savedHome = Environment.GetEnvironmentVariable("NETAGENTS_HOME");

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("NETAGENTS_HOME", _savedHome);
    }

    [Fact]
    public void ReturnsProjectScope_WhenAgentsTomlExists()
    {
        using var tmp = new TempDir();
        File.WriteAllText(tmp.Sub("agents.toml"), "");

        var s = ScopeResolver.ResolveDefaultScope(tmp.Path);

        s.Scope.Should().Be(ScopeKind.Project);
        s.Root.Should().Be(tmp.Path);
    }

    [Fact]
    public void FallsBackToUserScope_WhenNotInGitRepo()
    {
        using var tmp = new TempDir();
        Environment.SetEnvironmentVariable("NETAGENTS_HOME", tmp.Sub("user-home"));

        var stderr = new StringWriter();
        var prev = Console.Error;
        Console.SetError(stderr);
        try
        {
            var s = ScopeResolver.ResolveDefaultScope(tmp.Path);
            s.Scope.Should().Be(ScopeKind.User);
            stderr.ToString().Should().Contain("user scope");
        }
        finally
        {
            Console.SetError(prev);
        }
    }

    [Fact]
    public void ThrowsScopeException_WhenInGitRepoButNoAgentsToml()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(tmp.Sub(".git"));

        var ex = Assert.Throws<ScopeException>(() => ScopeResolver.ResolveDefaultScope(tmp.Path));
        ex.Message.Should().Contain("netagents init");
    }
}
