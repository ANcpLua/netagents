namespace NetAgents.Tests.Skills;

using AwesomeAssertions;
using NetAgents.Config;
using NetAgents.Skills;
using Xunit;

public class ApplyDefaultRepositorySourceTests
{
    [Fact]
    public void ExpandsShorthandToGithubByDefault()
    {
        SkillResolver.ApplyDefaultRepositorySource("getsentry/skills")
            .Should().Be("https://github.com/getsentry/skills");
    }

    [Fact]
    public void ExpandsShorthandToGitlabWhenConfigured()
    {
        SkillResolver.ApplyDefaultRepositorySource("getsentry/skills", RepositorySource.Gitlab)
            .Should().Be("https://gitlab.com/getsentry/skills");
    }

    [Fact]
    public void KeepsExplicitUrlUnchanged()
    {
        SkillResolver.ApplyDefaultRepositorySource("https://gitlab.com/group/repo")
            .Should().Be("https://gitlab.com/group/repo");
    }
}

public class ParseSourceTests
{
    [Fact]
    public void ParsesOwnerRepoAsGithub()
    {
        var result = SkillResolver.ParseSource("anthropics/skills");
        result.Type.Should().Be(SourceType.Github);
        result.Owner.Should().Be("anthropics");
        result.Repo.Should().Be("skills");
        result.Url.Should().Be("https://github.com/anthropics/skills.git");
        result.CloneUrl.Should().BeNull();
        result.Ref.Should().BeNull();
    }

    [Fact]
    public void ParsesOwnerRepoAtRefAsGithubWithRef()
    {
        var result = SkillResolver.ParseSource("getsentry/sentry-skills@v1.0.0");
        result.Type.Should().Be(SourceType.Github);
        result.Owner.Should().Be("getsentry");
        result.Repo.Should().Be("sentry-skills");
        result.Ref.Should().Be("v1.0.0");
        result.Url.Should().Be("https://github.com/getsentry/sentry-skills.git");
    }

    [Fact]
    public void ParsesOwnerRepoAtShaAsGithubWithShaRef()
    {
        var result = SkillResolver.ParseSource("anthropics/skills@abc123");
        result.Type.Should().Be(SourceType.Github);
        result.Ref.Should().Be("abc123");
    }

    [Fact]
    public void ParsesGitPrefixAsGenericGit()
    {
        var result = SkillResolver.ParseSource("git:https://git.corp.example.com/team/skills.git");
        result.Type.Should().Be(SourceType.Git);
        result.Url.Should().Be("https://git.corp.example.com/team/skills.git");
    }

    [Fact]
    public void ParsesPathPrefixAsLocal()
    {
        var result = SkillResolver.ParseSource("path:../shared/my-skill");
        result.Type.Should().Be(SourceType.Local);
        result.Path.Should().Be("../shared/my-skill");
    }

    [Fact]
    public void ParsesHttpsGithubUrl()
    {
        var result = SkillResolver.ParseSource("https://github.com/getsentry/skills");
        result.Type.Should().Be(SourceType.Github);
        result.Owner.Should().Be("getsentry");
        result.Repo.Should().Be("skills");
        result.Url.Should().Be("https://github.com/getsentry/skills.git");
        result.CloneUrl.Should().Be("https://github.com/getsentry/skills");
        result.Ref.Should().BeNull();
    }

    [Fact]
    public void ParsesHttpsGithubUrlWithGitSuffix()
    {
        var result = SkillResolver.ParseSource("https://github.com/getsentry/skills.git");
        result.Type.Should().Be(SourceType.Github);
        result.Owner.Should().Be("getsentry");
        result.Repo.Should().Be("skills");
        result.Url.Should().Be("https://github.com/getsentry/skills.git");
    }

    [Fact]
    public void ParsesHttpsGithubUrlWithTrailingSlash()
    {
        var result = SkillResolver.ParseSource("https://github.com/getsentry/skills/");
        result.Type.Should().Be(SourceType.Github);
        result.Owner.Should().Be("getsentry");
        result.Repo.Should().Be("skills");
    }

    [Fact]
    public void ParsesHttpsGithubUrlWithRef()
    {
        var result = SkillResolver.ParseSource("https://github.com/getsentry/skills@v1.0.0");
        result.Type.Should().Be(SourceType.Github);
        result.Owner.Should().Be("getsentry");
        result.Repo.Should().Be("skills");
        result.Ref.Should().Be("v1.0.0");
        result.Url.Should().Be("https://github.com/getsentry/skills.git");
        result.CloneUrl.Should().Be("https://github.com/getsentry/skills");
    }

    [Fact]
    public void ParsesSshGithubUrl()
    {
        var result = SkillResolver.ParseSource("git@github.com:getsentry/skills");
        result.Type.Should().Be(SourceType.Github);
        result.Owner.Should().Be("getsentry");
        result.Repo.Should().Be("skills");
        result.Url.Should().Be("https://github.com/getsentry/skills.git");
        result.CloneUrl.Should().Be("git@github.com:getsentry/skills");
        result.Ref.Should().BeNull();
    }

    [Fact]
    public void ParsesSshGithubUrlWithGitSuffix()
    {
        var result = SkillResolver.ParseSource("git@github.com:getsentry/skills.git");
        result.Type.Should().Be(SourceType.Github);
        result.Owner.Should().Be("getsentry");
        result.Repo.Should().Be("skills");
        result.Url.Should().Be("https://github.com/getsentry/skills.git");
        result.CloneUrl.Should().Be("git@github.com:getsentry/skills.git");
    }

    [Fact]
    public void ParsesSshGithubUrlWithRef()
    {
        var result = SkillResolver.ParseSource("git@github.com:getsentry/skills@v2.0");
        result.Type.Should().Be(SourceType.Github);
        result.Owner.Should().Be("getsentry");
        result.Repo.Should().Be("skills");
        result.Ref.Should().Be("v2.0");
        result.Url.Should().Be("https://github.com/getsentry/skills.git");
        result.CloneUrl.Should().Be("git@github.com:getsentry/skills");
    }

    [Fact]
    public void ParsesHttpsGithubUrlWithDottedRepoName()
    {
        var result = SkillResolver.ParseSource("https://github.com/vercel/next.js");
        result.Type.Should().Be(SourceType.Github);
        result.Owner.Should().Be("vercel");
        result.Repo.Should().Be("next.js");
        result.Url.Should().Be("https://github.com/vercel/next.js.git");
    }

    [Fact]
    public void ParsesHttpsGithubUrlWithDottedRepoNameAndGitSuffix()
    {
        var result = SkillResolver.ParseSource("https://github.com/vercel/next.js.git");
        result.Type.Should().Be(SourceType.Github);
        result.Owner.Should().Be("vercel");
        result.Repo.Should().Be("next.js");
        result.Url.Should().Be("https://github.com/vercel/next.js.git");
    }

    [Fact]
    public void ParsesHttpsGitlabUrl()
    {
        var result = SkillResolver.ParseSource("https://gitlab.com/group/repo");
        result.Type.Should().Be(SourceType.Git);
        result.Owner.Should().Be("group");
        result.Repo.Should().Be("repo");
        result.Url.Should().Be("https://gitlab.com/group/repo.git");
        result.CloneUrl.Should().Be("https://gitlab.com/group/repo");
    }

    [Fact]
    public void ParsesHttpsGitlabUrlWithSubgroup()
    {
        var result = SkillResolver.ParseSource("https://gitlab.com/group/subgroup/repo");
        result.Type.Should().Be(SourceType.Git);
        result.Owner.Should().Be("group/subgroup");
        result.Repo.Should().Be("repo");
        result.Url.Should().Be("https://gitlab.com/group/subgroup/repo.git");
    }

    [Fact]
    public void ParsesSshGitlabUrlWithRef()
    {
        var result = SkillResolver.ParseSource("git@gitlab.com:group/repo@v2.0");
        result.Type.Should().Be(SourceType.Git);
        result.Owner.Should().Be("group");
        result.Repo.Should().Be("repo");
        result.Ref.Should().Be("v2.0");
        result.Url.Should().Be("https://gitlab.com/group/repo.git");
        result.CloneUrl.Should().Be("git@gitlab.com:group/repo");
    }

    [Fact]
    public void UpgradesHttpToHttpsInCloneUrl()
    {
        var result = SkillResolver.ParseSource("http://github.com/getsentry/skills");
        result.Type.Should().Be(SourceType.Github);
        result.CloneUrl.Should().Be("https://github.com/getsentry/skills");
    }

    [Fact]
    public void DoesNotSetCloneUrlForOwnerRepoShorthand()
    {
        var result = SkillResolver.ParseSource("getsentry/skills@v1.0");
        result.CloneUrl.Should().BeNull();
    }

    [Fact]
    public void StripsRefContainingAtFromCloneUrlCorrectly()
    {
        var result = SkillResolver.ParseSource("git@github.com:org/repo@packages/foo@1.0.0");
        result.Type.Should().Be(SourceType.Github);
        result.Owner.Should().Be("org");
        result.Repo.Should().Be("repo");
        result.Ref.Should().Be("packages/foo@1.0.0");
        result.CloneUrl.Should().Be("git@github.com:org/repo");
    }
}

public class NormalizeSourceTests
{
    [Fact]
    public void NormalizesOwnerRepoShorthandToItself()
    {
        SkillResolver.NormalizeSource("getsentry/skills").Should().Be("getsentry/skills");
    }

    [Fact]
    public void NormalizesGithubHttpsUrlToOwnerRepo()
    {
        SkillResolver.NormalizeSource("https://github.com/getsentry/skills").Should().Be("getsentry/skills");
    }

    [Fact]
    public void NormalizesGithubSshUrlToOwnerRepo()
    {
        SkillResolver.NormalizeSource("git@github.com:getsentry/skills.git").Should().Be("getsentry/skills");
    }

    [Fact]
    public void NormalizesGithubHttpsUrlWithGitSuffix()
    {
        SkillResolver.NormalizeSource("https://github.com/getsentry/skills.git").Should().Be("getsentry/skills");
    }

    [Fact]
    public void NormalizesGitlabHttpsUrlToGroupRepo()
    {
        SkillResolver.NormalizeSource("https://gitlab.com/group/repo").Should().Be("group/repo");
    }

    [Fact]
    public void NormalizesGitlabSshUrlToGroupRepo()
    {
        SkillResolver.NormalizeSource("git@gitlab.com:group/repo.git").Should().Be("group/repo");
    }

    [Fact]
    public void ReturnsNonGithubSourcesUnchanged()
    {
        SkillResolver.NormalizeSource("path:../my-skill").Should().Be("path:../my-skill");
        SkillResolver.NormalizeSource("git:https://example.com/repo.git")
            .Should().Be("git:https://example.com/repo.git");
    }
}

public class SourcesMatchTests
{
    [Fact]
    public void MatchesIdenticalShorthand()
    {
        SkillResolver.SourcesMatch("getsentry/skills", "getsentry/skills").Should().BeTrue();
    }

    [Fact]
    public void MatchesSshUrlWithShorthand()
    {
        SkillResolver.SourcesMatch("git@github.com:getsentry/skills.git", "getsentry/skills").Should().BeTrue();
    }

    [Fact]
    public void MatchesHttpsUrlWithShorthand()
    {
        SkillResolver.SourcesMatch("https://github.com/getsentry/skills", "getsentry/skills").Should().BeTrue();
    }

    [Fact]
    public void MatchesGitlabUrlWithShorthand()
    {
        SkillResolver.SourcesMatch("https://gitlab.com/getsentry/skills", "getsentry/skills").Should().BeTrue();
    }

    [Fact]
    public void MatchesSshUrlWithHttpsUrl()
    {
        SkillResolver.SourcesMatch(
            "git@github.com:getsentry/skills.git",
            "https://github.com/getsentry/skills").Should().BeTrue();
    }

    [Fact]
    public void MatchesGitlabSshUrlWithGitlabHttpsUrl()
    {
        SkillResolver.SourcesMatch(
            "git@gitlab.com:group/repo.git",
            "https://gitlab.com/group/repo").Should().BeTrue();
    }

    [Fact]
    public void DoesNotMatchDifferentRepos()
    {
        SkillResolver.SourcesMatch("getsentry/skills", "getsentry/other").Should().BeFalse();
    }

    [Fact]
    public void DoesNotMatchDifferentOwners()
    {
        SkillResolver.SourcesMatch("getsentry/skills", "anthropics/skills").Should().BeFalse();
    }
}
