namespace NetAgents.Tests.Cli;

using AwesomeAssertions;
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
        config.Version.Should().Be(1);
    }

    [Fact]
    public async Task IncludesBootstrapSkill_ByDefault()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        config.Skills.Should().ContainSingle();
        var skill = config.Skills[0].Should().BeOfType<RegularSkillDependency>().Which;
        skill.Name.Should().Be("netagents");
        skill.Source.Should().Be("getsentry/dotagents");
    }

    [Fact]
    public async Task OmitsBootstrapSkill_WhenEmptySkillsProvided()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope, Skills: []), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        config.Skills.Should().BeEmpty();
    }

    [Fact]
    public async Task CreatesAgentsSkillsDirectory()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope, Skills: []), CT);

        Directory.Exists(Path.Combine(tmp.Path, ".agents", "skills")).Should().BeTrue();
    }

    [Fact]
    public async Task CreatesAgentsGitignore()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope, Skills: []), CT);

        File.Exists(Path.Combine(tmp.Path, ".agents", ".gitignore")).Should().BeTrue();
    }

    [Fact]
    public async Task ThrowsInitException_WhenAgentsTomlExists_WithoutForce()
    {
        using var tmp = new TempDir();
        await File.WriteAllTextAsync(Path.Combine(tmp.Path, "agents.toml"), "version = 1\n", CT);
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var ex = await Assert.ThrowsAsync<InitException>(() =>
            InitCommand.RunInitAsync(new InitOptions(scope, Skills: []), CT));
        ex.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task OverwritesAgentsToml_WithForce()
    {
        using var tmp = new TempDir();
        await File.WriteAllTextAsync(Path.Combine(tmp.Path, "agents.toml"), "garbage content", CT);
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope, true, Skills: []), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        config.Version.Should().Be(1);
    }

    [Fact]
    public async Task IsIdempotent_WithForce()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope, Skills: []), CT);
        await InitCommand.RunInitAsync(new InitOptions(scope, true, Skills: []), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        config.Version.Should().Be(1);
        Directory.Exists(scope.SkillsDir).Should().BeTrue();
    }

    [Fact]
    public async Task CreatesAllExpectedFilesAndDirectories()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope, Skills: []), CT);

        File.Exists(Path.Combine(tmp.Path, "agents.toml")).Should().BeTrue();
        Directory.Exists(Path.Combine(tmp.Path, ".agents")).Should().BeTrue();
        Directory.Exists(Path.Combine(tmp.Path, ".agents", "skills")).Should().BeTrue();
        File.Exists(Path.Combine(tmp.Path, ".agents", ".gitignore")).Should().BeTrue();
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
        entries.Should().Contain("my-skill");
    }

    [Fact]
    public async Task WritesAgentsField_WhenProvided()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope, Agents: ["claude", "cursor"], Skills: []), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        config.Agents.Should().BeEquivalentTo(["claude", "cursor"]);
    }

    [Fact]
    public async Task RejectsUnknownAgentIds()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var ex = await Assert.ThrowsAsync<InitException>(() =>
            InitCommand.RunInitAsync(new InitOptions(scope, Agents: ["emacs"], Skills: []), CT));
        ex.Message.Should().Contain("Unknown agent");
    }

    [Fact]
    public async Task AddsAgentsLockAndGitignore_ToRootGitignore()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope, Skills: []), CT);

        var content = await File.ReadAllTextAsync(Path.Combine(tmp.Path, ".gitignore"), CT);
        content.Should().Contain("agents.lock");
        content.Should().Contain(".agents/.gitignore");
    }

    [Fact]
    public async Task AppendsToExistingRootGitignore()
    {
        using var tmp = new TempDir();
        await File.WriteAllTextAsync(Path.Combine(tmp.Path, ".gitignore"), "node_modules/\n", CT);
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope, Skills: []), CT);

        var content = await File.ReadAllTextAsync(Path.Combine(tmp.Path, ".gitignore"), CT);
        content.Should().Contain("node_modules/");
        content.Should().Contain("agents.lock");
    }

    [Fact]
    public async Task WritesTrustSection_WhenProvided()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);
        var trust = new TrustConfig(false, ["my-org"], [], []);

        await InitCommand.RunInitAsync(new InitOptions(scope, Trust: trust, Skills: []), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        config.Trust!.GithubOrgs.Should().Contain("my-org");
    }

    [Fact]
    public async Task WritesAllowAllTrust_WhenSpecified()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);
        var trust = new TrustConfig(true, [], [], []);

        await InitCommand.RunInitAsync(new InitOptions(scope, Trust: trust, Skills: []), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        config.Trust!.AllowAll.Should().BeTrue();
    }

    [Fact]
    public async Task AutoWhitelists_GetsentryDotagents_InRestrictedTrust()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);
        var trust = new TrustConfig(false, ["my-org"], [], []);

        await InitCommand.RunInitAsync(new InitOptions(scope, Trust: trust), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        config.Trust!.GithubRepos.Should().Contain("getsentry/dotagents");
    }

    [Fact]
    public async Task DoesNotDuplicateWhitelist_WhenGetsentryOrgTrusted()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);
        var trust = new TrustConfig(false, ["getsentry"], [], []);

        await InitCommand.RunInitAsync(new InitOptions(scope, Trust: trust), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        config.Trust!.GithubRepos.Should().NotContain("getsentry/dotagents");
    }

    [Fact]
    public async Task DoesNotDuplicateWhitelist_WhenRepoAlreadyListed()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);
        var trust = new TrustConfig(false, [], ["getsentry/dotagents"], []);

        await InitCommand.RunInitAsync(new InitOptions(scope, Trust: trust), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        config.Trust!.GithubRepos.Count(r => r == "getsentry/dotagents").Should().Be(1);
    }

    [Fact]
    public async Task DoesNotWhitelist_WhenTrustIsAllowAll()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);
        var trust = new TrustConfig(true, [], [], []);

        await InitCommand.RunInitAsync(new InitOptions(scope, Trust: trust), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        config.Trust!.GithubRepos.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotWhitelist_WhenBootstrapSkillOmitted()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);
        var trust = new TrustConfig(false, ["my-org"], [], []);

        await InitCommand.RunInitAsync(new InitOptions(scope, Trust: trust, Skills: []), CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        config.Trust!.GithubRepos.Should().NotContain("getsentry/dotagents");
    }

    [Fact]
    public async Task GeneratedConfigHasNoPinField()
    {
        using var tmp = new TempDir();
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await InitCommand.RunInitAsync(new InitOptions(scope, Skills: []), CT);

        var raw = await File.ReadAllTextAsync(scope.ConfigPath, CT);
        raw.Should().NotContain("pin");
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

        result.Should().Be("created");
        var content = await File.ReadAllTextAsync(Path.Combine(gitDir, "hooks", "post-merge"), CT);
        content.Should().StartWith("#!/bin/sh");
        content.Should().Contain("netagents install");
        content.Should().Contain("netagents:post-merge");
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
            mode.HasFlag(UnixFileMode.UserExecute).Should().BeTrue();
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

        result.Should().Be("created");
        var content = await File.ReadAllTextAsync(Path.Combine(hooksDir, "post-merge"), CT);
        content.Should().Contain("echo 'existing'");
        content.Should().Contain("netagents install");
        // Only one shebang
        content.Split('\n').Count(l => l.StartsWith("#!/bin/sh")).Should().Be(1);
    }

    [Fact]
    public async Task ReturnsExists_IfMarkerAlreadyPresent()
    {
        using var tmp = new TempDir();
        var gitDir = Path.Combine(tmp.Path, ".git");
        Directory.CreateDirectory(gitDir);

        await InitCommand.InstallPostMergeHookAsync(gitDir, CT);
        var result = await InitCommand.InstallPostMergeHookAsync(gitDir, CT);

        result.Should().Be("exists");
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
        (content.Split("netagents:post-merge").Length - 1).Should().Be(1);
    }

    [Fact]
    public async Task IncludesDotnetToolFallback()
    {
        using var tmp = new TempDir();
        var gitDir = Path.Combine(tmp.Path, ".git");
        Directory.CreateDirectory(gitDir);

        await InitCommand.InstallPostMergeHookAsync(gitDir, CT);

        var content = await File.ReadAllTextAsync(Path.Combine(gitDir, "hooks", "post-merge"), CT);
        content.Should().Contain("dotnet tool run netagents install");
    }
}
