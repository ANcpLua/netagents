using System;
using System.IO;
using Xunit;

namespace NetAgents.Tests;

/// Shared temp-dir lifetime for test classes that need a disposable directory.
file sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());

    public TempDir() => Directory.CreateDirectory(Path);

    public string Sub(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public void Dispose()
    {
        if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
    }
}

// ── resolveScope ──────────────────────────────────────────────────────────────

public sealed class ResolveScopeTests : IDisposable
{
    private readonly string? _savedHome = Environment.GetEnvironmentVariable("NETAGENTS_HOME");

    public void Dispose() => Environment.SetEnvironmentVariable("NETAGENTS_HOME", _savedHome);

    [Fact]
    public void ProjectScope_UsesProjectRoot()
    {
        var s = ScopeResolver.ResolveScope(ScopeKind.Project, "/tmp/my-project");

        Assert.Equal(ScopeKind.Project, s.Scope);
        Assert.Equal("/tmp/my-project", s.Root);
        Assert.Equal(Path.Combine("/tmp/my-project", ".agents"), s.AgentsDir);
        Assert.Equal(Path.Combine("/tmp/my-project", "agents.toml"), s.ConfigPath);
        Assert.Equal(Path.Combine("/tmp/my-project", "agents.lock"), s.LockPath);
        Assert.Equal(Path.Combine("/tmp/my-project", ".agents", "skills"), s.SkillsDir);
    }

    [Fact]
    public void UserScope_UsesHomeDirByDefault()
    {
        Environment.SetEnvironmentVariable("NETAGENTS_HOME", null);
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agents");

        var s = ScopeResolver.ResolveScope(ScopeKind.User);

        Assert.Equal(ScopeKind.User, s.Scope);
        Assert.Equal(expected, s.Root);
        Assert.Equal(expected, s.AgentsDir);
        Assert.Equal(Path.Combine(expected, "agents.toml"), s.ConfigPath);
        Assert.Equal(Path.Combine(expected, "agents.lock"), s.LockPath);
        Assert.Equal(Path.Combine(expected, "skills"), s.SkillsDir);
    }

    [Fact]
    public void UserScope_RespectsNetagentsHomeOverride()
    {
        Environment.SetEnvironmentVariable("NETAGENTS_HOME", "/tmp/fake-home");
        var s = ScopeResolver.ResolveScope(ScopeKind.User);

        Assert.Equal("/tmp/fake-home", s.Root);
        Assert.Equal("/tmp/fake-home", s.AgentsDir);
        Assert.Equal(Path.Combine("/tmp/fake-home", "skills"), s.SkillsDir);
    }

    [Fact]
    public void UserScope_AgentsDirEqualsRoot()
    {
        Environment.SetEnvironmentVariable("NETAGENTS_HOME", "/tmp/user-agents");
        var s = ScopeResolver.ResolveScope(ScopeKind.User);

        Assert.Equal(s.Root, s.AgentsDir);
    }

    [Fact]
    public void ProjectScope_DefaultsToCwdWhenNoProjectRootGiven()
    {
        var s = ScopeResolver.ResolveScope(ScopeKind.Project);

        Assert.Equal(Directory.GetCurrentDirectory(), s.Root);
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

        Assert.True(ScopeResolver.IsInsideGitRepo(tmp.Path));
    }

    [Fact]
    public void ReturnsTrueWhenDotGitExistsInParent()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(tmp.Sub(".git"));
        var child = tmp.Sub("sub", "deep");
        Directory.CreateDirectory(child);

        Assert.True(ScopeResolver.IsInsideGitRepo(child));
    }

    [Fact]
    public void ReturnsFalseWhenNoDotGitInAnyParent()
    {
        using var tmp = new TempDir();

        Assert.False(ScopeResolver.IsInsideGitRepo(tmp.Path));
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

        Assert.Equal(tmp.Sub(".git"), ScopeResolver.FindGitDir(tmp.Path));
    }

    [Fact]
    public void ResolvesDotGitFile_SubmoduleToRealGitDir()
    {
        using var tmp = new TempDir();
        var realGitDir = tmp.Sub("real-git-dir");
        Directory.CreateDirectory(realGitDir);
        File.WriteAllText(tmp.Sub(".git"), $"gitdir: {realGitDir}\n");

        Assert.Equal(realGitDir, ScopeResolver.FindGitDir(tmp.Path));
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

        Assert.Equal(mainGitDir, ScopeResolver.FindGitDir(worktreeDir));
    }

    [Fact]
    public void ReturnsNull_ForDotGitFileWithInvalidGitdirTarget()
    {
        using var tmp = new TempDir();
        File.WriteAllText(tmp.Sub(".git"), "gitdir: /nonexistent/path\n");

        Assert.Null(ScopeResolver.FindGitDir(tmp.Path));
    }

    [Fact]
    public void ReturnsNull_WhenNoDotGitExists()
    {
        using var tmp = new TempDir();

        Assert.Null(ScopeResolver.FindGitDir(tmp.Path));
    }
}

// ── resolveDefaultScope ───────────────────────────────────────────────────────

public sealed class ResolveDefaultScopeTests : IDisposable
{
    private readonly string? _savedHome = Environment.GetEnvironmentVariable("NETAGENTS_HOME");

    public void Dispose() => Environment.SetEnvironmentVariable("NETAGENTS_HOME", _savedHome);

    [Fact]
    public void ReturnsProjectScope_WhenAgentsTomlExists()
    {
        using var tmp = new TempDir();
        File.WriteAllText(tmp.Sub("agents.toml"), "");

        var s = ScopeResolver.ResolveDefaultScope(tmp.Path);

        Assert.Equal(ScopeKind.Project, s.Scope);
        Assert.Equal(tmp.Path, s.Root);
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
            Assert.Equal(ScopeKind.User, s.Scope);
            Assert.Contains("user scope", stderr.ToString());
        }
        finally { Console.SetError(prev); }
    }

    [Fact]
    public void ThrowsScopeException_WhenInGitRepoButNoAgentsToml()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(tmp.Sub(".git"));

        var ex = Assert.Throws<ScopeException>(() => ScopeResolver.ResolveDefaultScope(tmp.Path));
        Assert.Contains("netagents init", ex.Message);
    }
}
