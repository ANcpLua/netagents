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

public sealed class ClassifyTrustSourceTests
{
    [Fact]
    public void ClassifiesOwnerRepo_AsGithubRepos()
    {
        var (field, value) = TrustCommand.ClassifyTrustSource("external-org/specific-repo");
        field.Should().Be("github_repos");
        value.Should().Be("external-org/specific-repo");
    }

    [Fact]
    public void ClassifiesDomain_AsGitDomains()
    {
        var (field, value) = TrustCommand.ClassifyTrustSource("git.corp.example.com");
        field.Should().Be("git_domains");
        value.Should().Be("git.corp.example.com");
    }

    [Fact]
    public void ClassifiesBareName_AsGithubOrgs()
    {
        var (field, value) = TrustCommand.ClassifyTrustSource("getsentry");
        field.Should().Be("github_orgs");
        value.Should().Be("getsentry");
    }

    [Fact]
    public void PrefersSlash_OverDotForClassification()
    {
        var (field, _) = TrustCommand.ClassifyTrustSource("owner.co/repo");
        field.Should().Be("github_repos");
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
        config.Trust!.GithubOrgs.Should().Contain("getsentry");
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
        config.Trust!.GithubOrgs.Should().BeEquivalentTo(["getsentry", "anthropics"]);
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
        config.Trust!.GithubOrgs.Should().BeEquivalentTo(["getsentry"]);
        config.Trust.GithubRepos.Should().Contain("external/repo");
    }

    [Fact]
    public async Task RejectsDuplicates_CaseInsensitive()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);
        await File.WriteAllTextAsync(scope.ConfigPath,
            "version = 1\n\n[trust]\ngithub_orgs = [\"getsentry\"]\n", CT);

        var ex = await Assert.ThrowsAsync<TrustCommandException>(() =>
            TrustCommand.RunTrustAddAsync(scope, "GetSentry", CT));
        ex.Message.Should().Contain("already in");
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
        config.Trust!.GithubOrgs.Should().BeEquivalentTo(["anthropics"]);
    }

    [Fact]
    public async Task ThrowsForNonExistentSource()
    {
        using var tmp = new TempDir();
        var scope = SetupScope(tmp.Path);
        await File.WriteAllTextAsync(scope.ConfigPath,
            "version = 1\n\n[trust]\ngithub_orgs = [\"getsentry\"]\n", CT);

        var ex = await Assert.ThrowsAsync<TrustCommandException>(() =>
            TrustCommand.RunTrustRemoveAsync(scope, "nope", CT));
        ex.Message.Should().Contain("not found");
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
        result.Should().BeAssignableTo<IReadOnlyList<TrustCommand.TrustListEntry>>();
        ((IReadOnlyList<TrustCommand.TrustListEntry>)result).Should().BeEmpty();
    }

    [Fact]
    public async Task ReturnsAllowAll_WhenSet()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(tmp.Path);
        var configPath = Path.Combine(tmp.Path, "agents.toml");
        await File.WriteAllTextAsync(configPath, "version = 1\n\n[trust]\nallow_all = true\n", CT);

        var config = await ConfigLoader.LoadAsync(configPath, CT);
        TrustCommand.GetTrustList(config).Should().Be("allow_all");
    }

    [Fact]
    public async Task ReturnsEntries_WithTypeLabels()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(tmp.Path);
        var configPath = Path.Combine(tmp.Path, "agents.toml");
        await File.WriteAllTextAsync(configPath,
            "version = 1\n\n[trust]\ngithub_orgs = [\"getsentry\"]\ngithub_repos = [\"ext/repo\"]\ngit_domains = [\"git.corp.com\"]\n",
            CT);

        var config = await ConfigLoader.LoadAsync(configPath, CT);
        var entries = (IReadOnlyList<TrustCommand.TrustListEntry>)TrustCommand.GetTrustList(config);
        entries.Count.Should().Be(3);
        entries[0].Should().Be(new TrustCommand.TrustListEntry("github_org", "getsentry"));
        entries[1].Should().Be(new TrustCommand.TrustListEntry("github_repo", "ext/repo"));
        entries[2].Should().Be(new TrustCommand.TrustListEntry("git_domain", "git.corp.com"));
    }
}
