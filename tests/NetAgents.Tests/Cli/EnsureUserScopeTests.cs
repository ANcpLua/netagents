namespace NetAgents.Tests.Cli;

using NetAgents.Agents;
using NetAgents.Cli;
using Xunit;

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
}

public sealed class EnsureUserScopeTests
{
    private static ScopeRoot UserScope(string home)
    {
        return new ScopeRoot(ScopeKind.User, home, home,
            Path.Combine(home, "agents.toml"),
            Path.Combine(home, "agents.lock"),
            Path.Combine(home, "skills"));
    }

    [Fact]
    public async Task CreatesAgentsTomlAndSkillsDir_WhenUninitialized()
    {
        using var tmp = new TempDir();
        var scope = UserScope(tmp.Path);

        await EnsureUserScope.EnsureUserScopeBootstrappedAsync(scope, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(scope.ConfigPath));
        Assert.True(Directory.Exists(scope.SkillsDir));

        var content = await File.ReadAllTextAsync(scope.ConfigPath, TestContext.Current.CancellationToken);
        Assert.Contains("version = 1", content);

        foreach (var id in AgentRegistry.AllAgentIds())
            Assert.Contains($"\"{id}\"", content);
    }

    [Fact]
    public async Task IsNoOp_WhenAgentsTomlAlreadyExists()
    {
        using var tmp = new TempDir();
        var scope = UserScope(tmp.Path);

        await EnsureUserScope.EnsureUserScopeBootstrappedAsync(scope, TestContext.Current.CancellationToken);
        var content = await File.ReadAllTextAsync(scope.ConfigPath, TestContext.Current.CancellationToken);

        await EnsureUserScope.EnsureUserScopeBootstrappedAsync(scope, TestContext.Current.CancellationToken);
        var contentAfter = await File.ReadAllTextAsync(scope.ConfigPath, TestContext.Current.CancellationToken);

        Assert.Equal(content, contentAfter);
    }

    [Fact]
    public async Task IsNoOp_ForProjectScope()
    {
        using var tmp = new TempDir();
        var scope = new ScopeRoot(ScopeKind.Project, tmp.Path,
            Path.Combine(tmp.Path, ".agents"),
            Path.Combine(tmp.Path, "agents.toml"),
            Path.Combine(tmp.Path, "agents.lock"),
            Path.Combine(tmp.Path, ".agents", "skills"));

        await EnsureUserScope.EnsureUserScopeBootstrappedAsync(scope, TestContext.Current.CancellationToken);

        Assert.False(File.Exists(scope.ConfigPath));
    }
}
