namespace NetAgents.Tests.Cli;

using NetAgents.Cli.Commands;
using NetAgents.Config;
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

public sealed class InitCommandTests
{
    private static CancellationToken CT => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreatesAgentsToml_InProjectRoot()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope, Skills: []), CT);

        var config = await ConfigLoader.LoadAsync(Path.Combine(tmp.Path, "agents.toml"), CT);
        Assert.Equal(1, config.Version);
    }

    [Fact]
    public async Task IncludesBootstrapSkill_ByDefault()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        Assert.Single(config.Skills);
        var skill = Assert.IsType<RegularSkillDependency>(config.Skills[0]);
        Assert.Equal("netagents", skill.Name);
        Assert.Equal("getsentry/dotagents", skill.Source);
    }

    [Fact]
    public async Task OmitsBootstrapSkill_WhenEmptySkillsProvided()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope, Skills: []), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        Assert.Empty(config.Skills);
    }

    [Fact]
    public async Task CreatesAgentsSkillsDirectory()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope, Skills: []), CT);

        Assert.True(Directory.Exists(Path.Combine(tmp.Path, ".agents", "skills")));
    }

    [Fact]
    public async Task CreatesAgentsGitignore()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope, Skills: []), CT);

        Assert.True(File.Exists(Path.Combine(tmp.Path, ".agents", ".gitignore")));
    }

    [Fact]
    public async Task ThrowsInitException_WhenAgentsTomlExists_WithoutForce()
    {
        using var tmp = new TempDir();
        await File.WriteAllTextAsync(Path.Combine(tmp.Path, "agents.toml"), "version = 1\n", CT);
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var ex = await Assert.ThrowsAsync<InitException>(() =>
            InitCommand.RunInitAsync(new InitOptions(scope, Skills: []), CT));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task OverwritesAgentsToml_WithForce()
    {
        using var tmp = new TempDir();
        await File.WriteAllTextAsync(Path.Combine(tmp.Path, "agents.toml"), "garbage content", CT);
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope, true, Skills: []), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        Assert.Equal(1, config.Version);
    }

    [Fact]
    public async Task IsIdempotent_WithForce()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope, Skills: []), CT);
        await InitCommand.RunInitAsync(new InitOptions(scope, true, Skills: []), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        Assert.Equal(1, config.Version);
        Assert.True(Directory.Exists(scope.SkillsDir));
    }

    [Fact]
    public async Task CreatesAllExpectedFilesAndDirectories()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope, Skills: []), CT);

        Assert.True(File.Exists(Path.Combine(tmp.Path, "agents.toml")));
        Assert.True(Directory.Exists(Path.Combine(tmp.Path, ".agents")));
        Assert.True(Directory.Exists(Path.Combine(tmp.Path, ".agents", "skills")));
        Assert.True(File.Exists(Path.Combine(tmp.Path, ".agents", ".gitignore")));
    }

    [Fact]
    public async Task PreservesExistingSkillsContents()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".agents", "skills", "my-skill"));
        await File.WriteAllTextAsync(
            Path.Combine(tmp.Path, ".agents", "skills", "my-skill", "SKILL.md"), "# test", CT);
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope, Skills: []), CT);

        var entries = Directory.GetDirectories(Path.Combine(tmp.Path, ".agents", "skills"))
            .Select(Path.GetFileName).ToList();
        Assert.Contains("my-skill", entries);
    }

    [Fact]
    public async Task WritesAgentsField_WhenProvided()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope, Agents: ["claude", "cursor"], Skills: []), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        Assert.Equal(["claude", "cursor"], config.Agents);
    }

    [Fact]
    public async Task RejectsUnknownAgentIds()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var ex = await Assert.ThrowsAsync<InitException>(() =>
            InitCommand.RunInitAsync(new InitOptions(scope, Agents: ["emacs"], Skills: []), CT));
        Assert.Contains("Unknown agent", ex.Message);
    }

    [Fact]
    public async Task AddsAgentsLockAndGitignore_ToRootGitignore()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope, Skills: []), CT);

        var content = await File.ReadAllTextAsync(Path.Combine(tmp.Path, ".gitignore"), CT);
        Assert.Contains("agents.lock", content);
        Assert.Contains(".agents/.gitignore", content);
    }

    [Fact]
    public async Task AppendsToExistingRootGitignore()
    {
        using var tmp = new TempDir();
        await File.WriteAllTextAsync(Path.Combine(tmp.Path, ".gitignore"), "node_modules/\n", CT);
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope, Skills: []), CT);

        var content = await File.ReadAllTextAsync(Path.Combine(tmp.Path, ".gitignore"), CT);
        Assert.Contains("node_modules/", content);
        Assert.Contains("agents.lock", content);
    }

    [Fact]
    public async Task WritesTrustSection_WhenProvided()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);
        var trust = new TrustConfig(false, ["my-org"], [], []);

        await InitCommand.RunInitAsync(new InitOptions(scope, Trust: trust, Skills: []), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        Assert.Contains("my-org", config.Trust!.GithubOrgs);
    }

    [Fact]
    public async Task WritesAllowAllTrust_WhenSpecified()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);
        var trust = new TrustConfig(true, [], [], []);

        await InitCommand.RunInitAsync(new InitOptions(scope, Trust: trust, Skills: []), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        Assert.True(config.Trust!.AllowAll);
    }

    [Fact]
    public async Task AutoWhitelists_GetsentryDotagents_InRestrictedTrust()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);
        var trust = new TrustConfig(false, ["my-org"], [], []);

        await InitCommand.RunInitAsync(new InitOptions(scope, Trust: trust), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        Assert.Contains("getsentry/dotagents", config.Trust!.GithubRepos);
    }

    [Fact]
    public async Task DoesNotDuplicateWhitelist_WhenGetsentryOrgTrusted()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);
        var trust = new TrustConfig(false, ["getsentry"], [], []);

        await InitCommand.RunInitAsync(new InitOptions(scope, Trust: trust), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        Assert.DoesNotContain("getsentry/dotagents", config.Trust!.GithubRepos);
    }

    [Fact]
    public async Task DoesNotDuplicateWhitelist_WhenRepoAlreadyListed()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);
        var trust = new TrustConfig(false, [], ["getsentry/dotagents"], []);

        await InitCommand.RunInitAsync(new InitOptions(scope, Trust: trust), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        Assert.Equal(1, config.Trust!.GithubRepos.Count(r => r == "getsentry/dotagents"));
    }

    [Fact]
    public async Task DoesNotWhitelist_WhenTrustIsAllowAll()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);
        var trust = new TrustConfig(true, [], [], []);

        await InitCommand.RunInitAsync(new InitOptions(scope, Trust: trust), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        Assert.Empty(config.Trust!.GithubRepos);
    }

    [Fact]
    public async Task DoesNotWhitelist_WhenBootstrapSkillOmitted()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);
        var trust = new TrustConfig(false, ["my-org"], [], []);

        await InitCommand.RunInitAsync(new InitOptions(scope, Trust: trust, Skills: []), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        Assert.DoesNotContain("getsentry/dotagents", config.Trust!.GithubRepos);
    }

    [Fact]
    public async Task GeneratedConfigHasNoPinField()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope, Skills: []), CT);

        var raw = await File.ReadAllTextAsync(scope.ConfigPath, CT);
        Assert.DoesNotContain("pin", raw);
    }
}

public sealed class InstallPostMergeHookTests
{
    private static CancellationToken CT => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreatesPostMergeHook_WithShebang()
    {
        using var tmp = new TempDir();
        var gitDir = Path.Combine(tmp.Path, ".git");
        Directory.CreateDirectory(gitDir);

        var result = await InitCommand.InstallPostMergeHookAsync(gitDir, CT);

        Assert.Equal("created", result);
        var content = await File.ReadAllTextAsync(Path.Combine(gitDir, "hooks", "post-merge"), CT);
        Assert.StartsWith("#!/bin/sh", content);
        Assert.Contains("netagents install", content);
        Assert.Contains("netagents:post-merge", content);
    }

    [Fact]
    public async Task MakesHookExecutable()
    {
        using var tmp = new TempDir();
        var gitDir = Path.Combine(tmp.Path, ".git");
        Directory.CreateDirectory(gitDir);

        await InitCommand.InstallPostMergeHookAsync(gitDir, CT);

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            var mode = File.GetUnixFileMode(Path.Combine(gitDir, "hooks", "post-merge"));
            Assert.True(mode.HasFlag(UnixFileMode.UserExecute));
        }
    }

    [Fact]
    public async Task AppendsToExistingHook_WithoutDuplicatingShebang()
    {
        using var tmp = new TempDir();
        var gitDir = Path.Combine(tmp.Path, ".git");
        var hooksDir = Path.Combine(gitDir, "hooks");
        Directory.CreateDirectory(hooksDir);
        await File.WriteAllTextAsync(Path.Combine(hooksDir, "post-merge"), "#!/bin/sh\necho 'existing'\n", CT);

        var result = await InitCommand.InstallPostMergeHookAsync(gitDir, CT);

        Assert.Equal("created", result);
        var content = await File.ReadAllTextAsync(Path.Combine(hooksDir, "post-merge"), CT);
        Assert.Contains("echo 'existing'", content);
        Assert.Contains("netagents install", content);
        // Only one shebang
        Assert.Equal(1, content.Split('\n').Count(l => l.StartsWith("#!/bin/sh")));
    }

    [Fact]
    public async Task ReturnsExists_IfMarkerAlreadyPresent()
    {
        using var tmp = new TempDir();
        var gitDir = Path.Combine(tmp.Path, ".git");
        Directory.CreateDirectory(gitDir);

        await InitCommand.InstallPostMergeHookAsync(gitDir, CT);
        var result = await InitCommand.InstallPostMergeHookAsync(gitDir, CT);

        Assert.Equal("exists", result);
    }

    [Fact]
    public async Task IsIdempotent_DoesNotDuplicateSnippet()
    {
        using var tmp = new TempDir();
        var gitDir = Path.Combine(tmp.Path, ".git");
        Directory.CreateDirectory(gitDir);

        await InitCommand.InstallPostMergeHookAsync(gitDir, CT);
        await InitCommand.InstallPostMergeHookAsync(gitDir, CT);

        var content = await File.ReadAllTextAsync(Path.Combine(gitDir, "hooks", "post-merge"), CT);
        Assert.Equal(1, content.Split("netagents:post-merge").Length - 1);
    }

    [Fact]
    public async Task IncludesDotnetToolFallback()
    {
        using var tmp = new TempDir();
        var gitDir = Path.Combine(tmp.Path, ".git");
        Directory.CreateDirectory(gitDir);

        await InitCommand.InstallPostMergeHookAsync(gitDir, CT);

        var content = await File.ReadAllTextAsync(Path.Combine(gitDir, "hooks", "post-merge"), CT);
        Assert.Contains("dotnet tool run netagents install", content);
    }
}
