using NetAgents.Cli.Commands;
using NetAgents.Config;
using Xunit;

namespace NetAgents.Tests.Cli;

file sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
    public TempDir() => Directory.CreateDirectory(Path);
    public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); }
}

public sealed class ClassifyTrustSourceTests
{
    [Fact]
    public void ClassifiesOwnerRepo_AsGithubRepos()
    {
        var (field, value) = TrustCommand.ClassifyTrustSource("external-org/specific-repo");
        Assert.Equal("github_repos", field);
        Assert.Equal("external-org/specific-repo", value);
    }

    [Fact]
    public void ClassifiesDomain_AsGitDomains()
    {
        var (field, value) = TrustCommand.ClassifyTrustSource("git.corp.example.com");
        Assert.Equal("git_domains", field);
        Assert.Equal("git.corp.example.com", value);
    }

    [Fact]
    public void ClassifiesBareName_AsGithubOrgs()
    {
        var (field, value) = TrustCommand.ClassifyTrustSource("getsentry");
        Assert.Equal("github_orgs", field);
        Assert.Equal("getsentry", value);
    }

    [Fact]
    public void PrefersSlash_OverDotForClassification()
    {
        var (field, _) = TrustCommand.ClassifyTrustSource("owner.co/repo");
        Assert.Equal("github_repos", field);
    }
}

public sealed class TrustAddTests
{
    private static CancellationToken CT => TestContext.Current.CancellationToken;

    private static ScopeRoot SetupScope(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, ".agents", "skills"));
        File.WriteAllText(Path.Combine(root, "agents.toml"), "version = 1\n");
        return new ScopeRoot(ScopeKind.Project, root,
            Path.Combine(root, ".agents"),
            Path.Combine(root, "agents.toml"),
            Path.Combine(root, "agents.lock"),
            Path.Combine(root, ".agents", "skills"));
    }

    [Fact]
    public async Task CreatesTrustSection_WhenAbsent()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);

        await TrustCommand.RunTrustAddAsync(scope, "getsentry", CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        Assert.Contains("getsentry", config.Trust!.GithubOrgs);
    }

    [Fact]
    public async Task AppendsToExistingTrustField()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);
        await File.WriteAllTextAsync(scope.ConfigPath,
            "version = 1\n\n[trust]\ngithub_orgs = [\"getsentry\"]\n", CT);

        await TrustCommand.RunTrustAddAsync(scope, "anthropics", CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        Assert.Equal(["getsentry", "anthropics"], config.Trust!.GithubOrgs);
    }

    [Fact]
    public async Task AddsNewField_ToExistingTrustSection()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);
        await File.WriteAllTextAsync(scope.ConfigPath,
            "version = 1\n\n[trust]\ngithub_orgs = [\"getsentry\"]\n", CT);

        await TrustCommand.RunTrustAddAsync(scope, "external/repo", CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        Assert.Equal(["getsentry"], config.Trust!.GithubOrgs);
        Assert.Contains("external/repo", config.Trust.GithubRepos);
    }

    [Fact]
    public async Task RejectsDuplicates_CaseInsensitive()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);
        await File.WriteAllTextAsync(scope.ConfigPath,
            "version = 1\n\n[trust]\ngithub_orgs = [\"getsentry\"]\n", CT);

        var ex = await Assert.ThrowsAsync<TrustCommandException>(
            () => TrustCommand.RunTrustAddAsync(scope, "GetSentry", CT));
        Assert.Contains("already in", ex.Message);
    }
}

public sealed class TrustRemoveTests
{
    private static CancellationToken CT => TestContext.Current.CancellationToken;

    private static ScopeRoot SetupScope(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, ".agents", "skills"));
        return new ScopeRoot(ScopeKind.Project, root,
            Path.Combine(root, ".agents"),
            Path.Combine(root, "agents.toml"),
            Path.Combine(root, "agents.lock"),
            Path.Combine(root, ".agents", "skills"));
    }

    [Fact]
    public async Task RemovesAnEntry()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);
        await File.WriteAllTextAsync(scope.ConfigPath,
            "version = 1\n\n[trust]\ngithub_orgs = [\"getsentry\", \"anthropics\"]\n", CT);

        await TrustCommand.RunTrustRemoveAsync(scope, "getsentry", CT);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        Assert.Equal(["anthropics"], config.Trust!.GithubOrgs);
    }

    [Fact]
    public async Task ThrowsForNonExistentSource()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);
        await File.WriteAllTextAsync(scope.ConfigPath,
            "version = 1\n\n[trust]\ngithub_orgs = [\"getsentry\"]\n", CT);

        var ex = await Assert.ThrowsAsync<TrustCommandException>(
            () => TrustCommand.RunTrustRemoveAsync(scope, "nope", CT));
        Assert.Contains("not found", ex.Message);
    }
}

public sealed class GetTrustListTests
{
    private static CancellationToken CT => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReturnsEmpty_ForNoTrustSection()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(tmp.Path);
        var configPath = Path.Combine(tmp.Path, "agents.toml");
        await File.WriteAllTextAsync(configPath, "version = 1\n", CT);

        var config = await ConfigLoader.LoadAsync(configPath, CT);
        var result = TrustCommand.GetTrustList(config);
        Assert.IsAssignableFrom<IReadOnlyList<TrustCommand.TrustListEntry>>(result);
        Assert.Empty((IReadOnlyList<TrustCommand.TrustListEntry>)result);
    }

    [Fact]
    public async Task ReturnsAllowAll_WhenSet()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(tmp.Path);
        var configPath = Path.Combine(tmp.Path, "agents.toml");
        await File.WriteAllTextAsync(configPath, "version = 1\n\n[trust]\nallow_all = true\n", CT);

        var config = await ConfigLoader.LoadAsync(configPath, CT);
        Assert.Equal("allow_all", TrustCommand.GetTrustList(config));
    }

    [Fact]
    public async Task ReturnsEntries_WithTypeLabels()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(tmp.Path);
        var configPath = Path.Combine(tmp.Path, "agents.toml");
        await File.WriteAllTextAsync(configPath,
            "version = 1\n\n[trust]\ngithub_orgs = [\"getsentry\"]\ngithub_repos = [\"ext/repo\"]\ngit_domains = [\"git.corp.com\"]\n", CT);

        var config = await ConfigLoader.LoadAsync(configPath, CT);
        var entries = (IReadOnlyList<TrustCommand.TrustListEntry>)TrustCommand.GetTrustList(config);
        Assert.Equal(3, entries.Count);
        Assert.Equal(new TrustCommand.TrustListEntry("github_org", "getsentry"), entries[0]);
        Assert.Equal(new TrustCommand.TrustListEntry("github_repo", "ext/repo"), entries[1]);
        Assert.Equal(new TrustCommand.TrustListEntry("git_domain", "git.corp.com"), entries[2]);
    }
}
