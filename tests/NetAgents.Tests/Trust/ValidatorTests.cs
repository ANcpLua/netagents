namespace NetAgents.Tests.Trust;

using AwesomeAssertions;
using NetAgents.Config;
using NetAgents.Trust;
using Xunit;

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

public class ValidateTrustedSourceTests
{
    [Fact]
    public void AllowsEverythingWhenTrustConfigIsNull()
    {
        TrustValidator.ValidateTrustedSource("evil/repo", null);
        TrustValidator.ValidateTrustedSource("git:https://evil.com/repo.git", null);
        TrustValidator.ValidateTrustedSource("path:../local", null);
    }

    [Fact]
    public void AllowsEverythingWhenAllowAllIsTrue()
    {
        var trust = TrustHelper.MakeTrust(true);
        TrustValidator.ValidateTrustedSource("evil/repo", trust);
        TrustValidator.ValidateTrustedSource("git:https://evil.com/repo.git", trust);
    }

    [Fact]
    public void AllowsEverythingWhenAllowAllIsTrueEvenWithOtherRules()
    {
        var trust = TrustHelper.MakeTrust(true, ["getsentry"]);
        TrustValidator.ValidateTrustedSource("evil/repo", trust);
    }
}

public class GithubOrgsTests
{
    private readonly TrustConfig _trust = TrustHelper.MakeTrust(githubOrgs: ["getsentry", "anthropics"]);

    [Fact]
    public void AllowsMatchingOrgs()
    {
        TrustValidator.ValidateTrustedSource("getsentry/skills", _trust);
        TrustValidator.ValidateTrustedSource("anthropics/tools", _trust);
    }

    [Fact]
    public void RejectsNonMatchingOrgs()
    {
        Assert.Throws<TrustException>(() => TrustValidator.ValidateTrustedSource("evil/repo", _trust));
    }

    [Fact]
    public void StripsRefBeforeChecking()
    {
        TrustValidator.ValidateTrustedSource("getsentry/skills@v1.0.0", _trust);
        Assert.Throws<TrustException>(() => TrustValidator.ValidateTrustedSource("evil/repo@main", _trust));
    }
}

public class GithubReposTests
{
    private readonly TrustConfig _trust = TrustHelper.MakeTrust(githubRepos: ["external-org/one-approved"]);

    [Fact]
    public void AllowsExactRepoMatches()
    {
        TrustValidator.ValidateTrustedSource("external-org/one-approved", _trust);
    }

    [Fact]
    public void RejectsSameOrgDifferentRepo()
    {
        Assert.Throws<TrustException>(() => TrustValidator.ValidateTrustedSource("external-org/other-repo", _trust));
    }

    [Fact]
    public void RejectsDifferentOrgSameRepo()
    {
        Assert.Throws<TrustException>(() => TrustValidator.ValidateTrustedSource("other-org/one-approved", _trust));
    }

    [Fact]
    public void StripsRefBeforeChecking()
    {
        TrustValidator.ValidateTrustedSource("external-org/one-approved@v2", _trust);
    }
}

public class GitDomainsTests
{
    private readonly TrustConfig _trust = TrustHelper.MakeTrust(gitDomains: ["git.corp.example.com"]);

    [Fact]
    public void AllowsMatchingDomainsHttps()
    {
        TrustValidator.ValidateTrustedSource("git:https://git.corp.example.com/team/repo.git", _trust);
    }

    [Fact]
    public void AllowsMatchingDomainsSsh()
    {
        TrustValidator.ValidateTrustedSource("git:ssh://git.corp.example.com/team/repo.git", _trust);
    }

    [Fact]
    public void AllowsMatchingDomainsScpStyle()
    {
        TrustValidator.ValidateTrustedSource("git:git@git.corp.example.com:team/repo.git", _trust);
    }

    [Fact]
    public void RejectsNonMatchingDomains()
    {
        Assert.Throws<TrustException>(() =>
            TrustValidator.ValidateTrustedSource("git:https://evil.com/repo.git", _trust));
    }

    [Fact]
    public void AllowsDirectGitLabUrlsWhenDomainIsTrusted()
    {
        var gitlabTrust = TrustHelper.MakeTrust(gitDomains: ["gitlab.com"]);
        TrustValidator.ValidateTrustedSource("https://gitlab.com/group/repo", gitlabTrust);
    }
}

public class LocalSourceTests
{
    [Fact]
    public void AlwaysAllowsPathSourcesEvenWithRestrictiveTrust()
    {
        var trust = TrustHelper.MakeTrust(githubOrgs: ["getsentry"]);
        TrustValidator.ValidateTrustedSource("path:../local-skill", trust);
    }
}

public class CombinedRulesTests
{
    private readonly TrustConfig _trust = TrustHelper.MakeTrust(
        githubOrgs: ["getsentry"],
        githubRepos: ["external/approved"],
        gitDomains: ["git.corp.com"]);

    [Fact]
    public void AllowsSourceMatchingOrgRule()
    {
        TrustValidator.ValidateTrustedSource("getsentry/anything", _trust);
    }

    [Fact]
    public void AllowsSourceMatchingRepoRule()
    {
        TrustValidator.ValidateTrustedSource("external/approved", _trust);
    }

    [Fact]
    public void AllowsSourceMatchingDomainRule()
    {
        TrustValidator.ValidateTrustedSource("git:https://git.corp.com/team/repo.git", _trust);
    }

    [Fact]
    public void RejectsSourceMatchingNone()
    {
        Assert.Throws<TrustException>(() => TrustValidator.ValidateTrustedSource("evil/repo", _trust));
    }
}

public class CaseInsensitiveMatchingTests
{
    [Fact]
    public void MatchesGithubOrgsCaseInsensitively()
    {
        var trust = TrustHelper.MakeTrust(githubOrgs: ["getsentry"]);
        TrustValidator.ValidateTrustedSource("GetSentry/repo", trust);
        TrustValidator.ValidateTrustedSource("GETSENTRY/repo", trust);
    }

    [Fact]
    public void MatchesGithubReposCaseInsensitively()
    {
        var trust = TrustHelper.MakeTrust(githubRepos: ["MyOrg/MyRepo"]);
        TrustValidator.ValidateTrustedSource("myorg/myrepo", trust);
        TrustValidator.ValidateTrustedSource("MYORG/MYREPO", trust);
    }

    [Fact]
    public void MatchesGitDomainsCaseInsensitively()
    {
        var trust = TrustHelper.MakeTrust(gitDomains: ["git.corp.example.com"]);
        TrustValidator.ValidateTrustedSource("git:https://Git.Corp.Example.COM/repo.git", trust);
    }
}

public class ErrorMessageTests
{
    [Fact]
    public void IncludesTheRejectedSource()
    {
        var trust = TrustHelper.MakeTrust(githubOrgs: ["getsentry"]);
        var ex = Assert.Throws<TrustException>(() => TrustValidator.ValidateTrustedSource("evil/repo", trust));
        ex.Message.Should().Contain("evil/repo");
    }

    [Fact]
    public void IncludesAllowedAlternatives()
    {
        var trust = TrustHelper.MakeTrust(githubOrgs: ["getsentry"], githubRepos: ["ext/one"]);
        var ex = Assert.Throws<TrustException>(() => TrustValidator.ValidateTrustedSource("evil/repo", trust));
        ex.Message.Should().Contain("getsentry");
        ex.Message.Should().Contain("ext/one");
    }

    [Fact]
    public void SuggestsNetagentsTrustAddForGithubSources()
    {
        var trust = TrustHelper.MakeTrust(githubOrgs: ["getsentry"]);
        var ex = Assert.Throws<TrustException>(() => TrustValidator.ValidateTrustedSource("evil/repo", trust));
        ex.Message.Should().Contain("netagents trust add evil");
        ex.Message.Should().Contain("netagents trust add evil/repo");
    }

    [Fact]
    public void SuggestsNetagentsTrustAddForGitDomainSources()
    {
        var trust = TrustHelper.MakeTrust(gitDomains: ["git.corp.com"]);
        var ex = Assert.Throws<TrustException>(() =>
            TrustValidator.ValidateTrustedSource("git:https://evil.com/repo.git", trust));
        ex.Message.Should().Contain("netagents trust add evil.com");
    }
}

public class ExtractDomainTests
{
    [Fact]
    public void ExtractsFromHttpsUrl()
    {
        TrustValidator.ExtractDomain("https://git.corp.com/team/repo.git").Should().Be("git.corp.com");
    }

    [Fact]
    public void ExtractsFromSshUrl()
    {
        TrustValidator.ExtractDomain("ssh://git.corp.com/team/repo.git").Should().Be("git.corp.com");
    }

    [Fact]
    public void ExtractsFromGitProtocolUrl()
    {
        TrustValidator.ExtractDomain("git://git.corp.com/team/repo.git").Should().Be("git.corp.com");
    }

    [Fact]
    public void ExtractsFromScpStyleUrl()
    {
        TrustValidator.ExtractDomain("git@github.com:owner/repo.git").Should().Be("github.com");
    }

    [Fact]
    public void ReturnsNullForFileUrls()
    {
        TrustValidator.ExtractDomain("file:///tmp/repo").Should().BeNull();
    }

    [Fact]
    public void ReturnsNullForBarePaths()
    {
        TrustValidator.ExtractDomain("/tmp/local-repo").Should().BeNull();
    }
}
