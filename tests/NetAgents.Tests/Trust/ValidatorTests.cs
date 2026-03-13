namespace NetAgents.Tests.Trust;

using NetAgents.Config;
using NetAgents.Trust;
using Xunit;

// ── Helpers ──────────────────────────────────────────────────────────────────

file static class TrustHelper
{
    public static TrustConfig MakeTrust(
        bool allowAll = false,
        IReadOnlyList<string>? githubOrgs = null,
        IReadOnlyList<string>? githubRepos = null,
        IReadOnlyList<string>? gitDomains = null)
    {
        return new TrustConfig(allowAll, githubOrgs ?? [], githubRepos ?? [], gitDomains ?? []);
    }
}

// ── ValidateTrustedSource ────────────────────────────────────────────────────

public class ValidateTrustedSourceTests
{
    [Fact]
    public async Task AllowsEverythingWhenTrustConfigIsNull()
    {
        await Task.CompletedTask;
        TrustValidator.ValidateTrustedSource("evil/repo", null);
        TrustValidator.ValidateTrustedSource("git:https://evil.com/repo.git", null);
        TrustValidator.ValidateTrustedSource("path:../local", null);
    }

    [Fact]
    public async Task AllowsEverythingWhenAllowAllIsTrue()
    {
        await Task.CompletedTask;
        var trust = TrustHelper.MakeTrust(true);
        TrustValidator.ValidateTrustedSource("evil/repo", trust);
        TrustValidator.ValidateTrustedSource("git:https://evil.com/repo.git", trust);
    }

    [Fact]
    public async Task AllowsEverythingWhenAllowAllIsTrueEvenWithOtherRules()
    {
        await Task.CompletedTask;
        var trust = TrustHelper.MakeTrust(true, ["getsentry"]);
        TrustValidator.ValidateTrustedSource("evil/repo", trust);
    }
}

// ── github_orgs ──────────────────────────────────────────────────────────────

public class GithubOrgsTests
{
    private readonly TrustConfig _trust = TrustHelper.MakeTrust(githubOrgs: ["getsentry", "anthropics"]);

    [Fact]
    public async Task AllowsMatchingOrgs()
    {
        await Task.CompletedTask;
        TrustValidator.ValidateTrustedSource("getsentry/skills", _trust);
        TrustValidator.ValidateTrustedSource("anthropics/tools", _trust);
    }

    [Fact]
    public async Task RejectsNonMatchingOrgs()
    {
        await Task.CompletedTask;
        Assert.Throws<TrustException>(() => TrustValidator.ValidateTrustedSource("evil/repo", _trust));
    }

    [Fact]
    public async Task StripsRefBeforeChecking()
    {
        await Task.CompletedTask;
        TrustValidator.ValidateTrustedSource("getsentry/skills@v1.0.0", _trust);
        Assert.Throws<TrustException>(() => TrustValidator.ValidateTrustedSource("evil/repo@main", _trust));
    }
}

// ── github_repos ─────────────────────────────────────────────────────────────

public class GithubReposTests
{
    private readonly TrustConfig _trust = TrustHelper.MakeTrust(githubRepos: ["external-org/one-approved"]);

    [Fact]
    public async Task AllowsExactRepoMatches()
    {
        await Task.CompletedTask;
        TrustValidator.ValidateTrustedSource("external-org/one-approved", _trust);
    }

    [Fact]
    public async Task RejectsSameOrgDifferentRepo()
    {
        await Task.CompletedTask;
        Assert.Throws<TrustException>(() => TrustValidator.ValidateTrustedSource("external-org/other-repo", _trust));
    }

    [Fact]
    public async Task RejectsDifferentOrgSameRepo()
    {
        await Task.CompletedTask;
        Assert.Throws<TrustException>(() => TrustValidator.ValidateTrustedSource("other-org/one-approved", _trust));
    }

    [Fact]
    public async Task StripsRefBeforeChecking()
    {
        await Task.CompletedTask;
        TrustValidator.ValidateTrustedSource("external-org/one-approved@v2", _trust);
    }
}

// ── git_domains ──────────────────────────────────────────────────────────────

public class GitDomainsTests
{
    private readonly TrustConfig _trust = TrustHelper.MakeTrust(gitDomains: ["git.corp.example.com"]);

    [Fact]
    public async Task AllowsMatchingDomainsHttps()
    {
        await Task.CompletedTask;
        TrustValidator.ValidateTrustedSource("git:https://git.corp.example.com/team/repo.git", _trust);
    }

    [Fact]
    public async Task AllowsMatchingDomainsSsh()
    {
        await Task.CompletedTask;
        TrustValidator.ValidateTrustedSource("git:ssh://git.corp.example.com/team/repo.git", _trust);
    }

    [Fact]
    public async Task AllowsMatchingDomainsScpStyle()
    {
        await Task.CompletedTask;
        TrustValidator.ValidateTrustedSource("git:git@git.corp.example.com:team/repo.git", _trust);
    }

    [Fact]
    public async Task RejectsNonMatchingDomains()
    {
        await Task.CompletedTask;
        Assert.Throws<TrustException>(() =>
            TrustValidator.ValidateTrustedSource("git:https://evil.com/repo.git", _trust));
    }

    [Fact]
    public async Task AllowsDirectGitLabUrlsWhenDomainIsTrusted()
    {
        await Task.CompletedTask;
        var gitlabTrust = TrustHelper.MakeTrust(gitDomains: ["gitlab.com"]);
        TrustValidator.ValidateTrustedSource("https://gitlab.com/group/repo", gitlabTrust);
    }
}

// ── local sources ────────────────────────────────────────────────────────────

public class LocalSourceTests
{
    [Fact]
    public async Task AlwaysAllowsPathSourcesEvenWithRestrictiveTrust()
    {
        await Task.CompletedTask;
        var trust = TrustHelper.MakeTrust(githubOrgs: ["getsentry"]);
        TrustValidator.ValidateTrustedSource("path:../local-skill", trust);
    }
}

// ── combined rules ───────────────────────────────────────────────────────────

public class CombinedRulesTests
{
    private readonly TrustConfig _trust = TrustHelper.MakeTrust(
        githubOrgs: ["getsentry"],
        githubRepos: ["external/approved"],
        gitDomains: ["git.corp.com"]);

    [Fact]
    public async Task AllowsSourceMatchingOrgRule()
    {
        await Task.CompletedTask;
        TrustValidator.ValidateTrustedSource("getsentry/anything", _trust);
    }

    [Fact]
    public async Task AllowsSourceMatchingRepoRule()
    {
        await Task.CompletedTask;
        TrustValidator.ValidateTrustedSource("external/approved", _trust);
    }

    [Fact]
    public async Task AllowsSourceMatchingDomainRule()
    {
        await Task.CompletedTask;
        TrustValidator.ValidateTrustedSource("git:https://git.corp.com/team/repo.git", _trust);
    }

    [Fact]
    public async Task RejectsSourceMatchingNone()
    {
        await Task.CompletedTask;
        Assert.Throws<TrustException>(() => TrustValidator.ValidateTrustedSource("evil/repo", _trust));
    }
}

// ── case-insensitive matching ────────────────────────────────────────────────

public class CaseInsensitiveMatchingTests
{
    [Fact]
    public async Task MatchesGithubOrgsCaseInsensitively()
    {
        await Task.CompletedTask;
        var trust = TrustHelper.MakeTrust(githubOrgs: ["getsentry"]);
        TrustValidator.ValidateTrustedSource("GetSentry/repo", trust);
        TrustValidator.ValidateTrustedSource("GETSENTRY/repo", trust);
    }

    [Fact]
    public async Task MatchesGithubReposCaseInsensitively()
    {
        await Task.CompletedTask;
        var trust = TrustHelper.MakeTrust(githubRepos: ["MyOrg/MyRepo"]);
        TrustValidator.ValidateTrustedSource("myorg/myrepo", trust);
        TrustValidator.ValidateTrustedSource("MYORG/MYREPO", trust);
    }

    [Fact]
    public async Task MatchesGitDomainsCaseInsensitively()
    {
        await Task.CompletedTask;
        var trust = TrustHelper.MakeTrust(gitDomains: ["git.corp.example.com"]);
        TrustValidator.ValidateTrustedSource("git:https://Git.Corp.Example.COM/repo.git", trust);
    }
}

// ── error messages ───────────────────────────────────────────────────────────

public class ErrorMessageTests
{
    [Fact]
    public async Task IncludesTheRejectedSource()
    {
        await Task.CompletedTask;
        var trust = TrustHelper.MakeTrust(githubOrgs: ["getsentry"]);
        var ex = Assert.Throws<TrustException>(() => TrustValidator.ValidateTrustedSource("evil/repo", trust));
        Assert.Contains("evil/repo", ex.Message);
    }

    [Fact]
    public async Task IncludesAllowedAlternatives()
    {
        await Task.CompletedTask;
        var trust = TrustHelper.MakeTrust(githubOrgs: ["getsentry"], githubRepos: ["ext/one"]);
        var ex = Assert.Throws<TrustException>(() => TrustValidator.ValidateTrustedSource("evil/repo", trust));
        Assert.Contains("getsentry", ex.Message);
        Assert.Contains("ext/one", ex.Message);
    }

    [Fact]
    public async Task SuggestsNetagentsTrustAddForGithubSources()
    {
        await Task.CompletedTask;
        var trust = TrustHelper.MakeTrust(githubOrgs: ["getsentry"]);
        var ex = Assert.Throws<TrustException>(() => TrustValidator.ValidateTrustedSource("evil/repo", trust));
        Assert.Contains("netagents trust add evil", ex.Message);
        Assert.Contains("netagents trust add evil/repo", ex.Message);
    }

    [Fact]
    public async Task SuggestsNetagentsTrustAddForGitDomainSources()
    {
        await Task.CompletedTask;
        var trust = TrustHelper.MakeTrust(gitDomains: ["git.corp.com"]);
        var ex = Assert.Throws<TrustException>(() =>
            TrustValidator.ValidateTrustedSource("git:https://evil.com/repo.git", trust));
        Assert.Contains("netagents trust add evil.com", ex.Message);
    }
}

// ── ExtractDomain ────────────────────────────────────────────────────────────

public class ExtractDomainTests
{
    [Fact]
    public async Task ExtractsFromHttpsUrl()
    {
        await Task.CompletedTask;
        Assert.Equal("git.corp.com", TrustValidator.ExtractDomain("https://git.corp.com/team/repo.git"));
    }

    [Fact]
    public async Task ExtractsFromSshUrl()
    {
        await Task.CompletedTask;
        Assert.Equal("git.corp.com", TrustValidator.ExtractDomain("ssh://git.corp.com/team/repo.git"));
    }

    [Fact]
    public async Task ExtractsFromGitProtocolUrl()
    {
        await Task.CompletedTask;
        Assert.Equal("git.corp.com", TrustValidator.ExtractDomain("git://git.corp.com/team/repo.git"));
    }

    [Fact]
    public async Task ExtractsFromScpStyleUrl()
    {
        await Task.CompletedTask;
        Assert.Equal("github.com", TrustValidator.ExtractDomain("git@github.com:owner/repo.git"));
    }

    [Fact]
    public async Task ReturnsNullForFileUrls()
    {
        await Task.CompletedTask;
        Assert.Null(TrustValidator.ExtractDomain("file:///tmp/repo"));
    }

    [Fact]
    public async Task ReturnsNullForBarePaths()
    {
        await Task.CompletedTask;
        Assert.Null(TrustValidator.ExtractDomain("/tmp/local-repo"));
    }
}
