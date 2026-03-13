namespace NetAgents.Tests.Skills;

using NetAgents.Config;
using NetAgents.Skills;
using Xunit;

public class ApplyDefaultRepositorySourceTests
{
    [Fact]
    public async Task ExpandsShorthandToGithubByDefault()
    {
        Assert.Equal(
            "https://github.com/getsentry/skills",
            SkillResolver.ApplyDefaultRepositorySource("getsentry/skills"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ExpandsShorthandToGitlabWhenConfigured()
    {
        Assert.Equal(
            "https://gitlab.com/getsentry/skills",
            SkillResolver.ApplyDefaultRepositorySource("getsentry/skills", RepositorySource.Gitlab));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task KeepsExplicitUrlUnchanged()
    {
        Assert.Equal(
            "https://gitlab.com/group/repo",
            SkillResolver.ApplyDefaultRepositorySource("https://gitlab.com/group/repo"));
        await Task.CompletedTask;
    }
}

public class ParseSourceTests
{
    [Fact]
    public async Task ParsesOwnerRepoAsGithub()
    {
        var result = SkillResolver.ParseSource("anthropics/skills");
        Assert.Equal(SourceType.Github, result.Type);
        Assert.Equal("anthropics", result.Owner);
        Assert.Equal("skills", result.Repo);
        Assert.Equal("https://github.com/anthropics/skills.git", result.Url);
        Assert.Null(result.CloneUrl);
        Assert.Null(result.Ref);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParsesOwnerRepoAtRefAsGithubWithRef()
    {
        var result = SkillResolver.ParseSource("getsentry/sentry-skills@v1.0.0");
        Assert.Equal(SourceType.Github, result.Type);
        Assert.Equal("getsentry", result.Owner);
        Assert.Equal("sentry-skills", result.Repo);
        Assert.Equal("v1.0.0", result.Ref);
        Assert.Equal("https://github.com/getsentry/sentry-skills.git", result.Url);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParsesOwnerRepoAtShaAsGithubWithShaRef()
    {
        var result = SkillResolver.ParseSource("anthropics/skills@abc123");
        Assert.Equal(SourceType.Github, result.Type);
        Assert.Equal("abc123", result.Ref);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParsesGitPrefixAsGenericGit()
    {
        var result = SkillResolver.ParseSource("git:https://git.corp.example.com/team/skills.git");
        Assert.Equal(SourceType.Git, result.Type);
        Assert.Equal("https://git.corp.example.com/team/skills.git", result.Url);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParsesPathPrefixAsLocal()
    {
        var result = SkillResolver.ParseSource("path:../shared/my-skill");
        Assert.Equal(SourceType.Local, result.Type);
        Assert.Equal("../shared/my-skill", result.Path);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParsesHttpsGithubUrl()
    {
        var result = SkillResolver.ParseSource("https://github.com/getsentry/skills");
        Assert.Equal(SourceType.Github, result.Type);
        Assert.Equal("getsentry", result.Owner);
        Assert.Equal("skills", result.Repo);
        Assert.Equal("https://github.com/getsentry/skills.git", result.Url);
        Assert.Equal("https://github.com/getsentry/skills", result.CloneUrl);
        Assert.Null(result.Ref);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParsesHttpsGithubUrlWithGitSuffix()
    {
        var result = SkillResolver.ParseSource("https://github.com/getsentry/skills.git");
        Assert.Equal(SourceType.Github, result.Type);
        Assert.Equal("getsentry", result.Owner);
        Assert.Equal("skills", result.Repo);
        Assert.Equal("https://github.com/getsentry/skills.git", result.Url);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParsesHttpsGithubUrlWithTrailingSlash()
    {
        var result = SkillResolver.ParseSource("https://github.com/getsentry/skills/");
        Assert.Equal(SourceType.Github, result.Type);
        Assert.Equal("getsentry", result.Owner);
        Assert.Equal("skills", result.Repo);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParsesHttpsGithubUrlWithRef()
    {
        var result = SkillResolver.ParseSource("https://github.com/getsentry/skills@v1.0.0");
        Assert.Equal(SourceType.Github, result.Type);
        Assert.Equal("getsentry", result.Owner);
        Assert.Equal("skills", result.Repo);
        Assert.Equal("v1.0.0", result.Ref);
        Assert.Equal("https://github.com/getsentry/skills.git", result.Url);
        Assert.Equal("https://github.com/getsentry/skills", result.CloneUrl);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParsesSshGithubUrl()
    {
        var result = SkillResolver.ParseSource("git@github.com:getsentry/skills");
        Assert.Equal(SourceType.Github, result.Type);
        Assert.Equal("getsentry", result.Owner);
        Assert.Equal("skills", result.Repo);
        Assert.Equal("https://github.com/getsentry/skills.git", result.Url);
        Assert.Equal("git@github.com:getsentry/skills", result.CloneUrl);
        Assert.Null(result.Ref);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParsesSshGithubUrlWithGitSuffix()
    {
        var result = SkillResolver.ParseSource("git@github.com:getsentry/skills.git");
        Assert.Equal(SourceType.Github, result.Type);
        Assert.Equal("getsentry", result.Owner);
        Assert.Equal("skills", result.Repo);
        Assert.Equal("https://github.com/getsentry/skills.git", result.Url);
        Assert.Equal("git@github.com:getsentry/skills.git", result.CloneUrl);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParsesSshGithubUrlWithRef()
    {
        var result = SkillResolver.ParseSource("git@github.com:getsentry/skills@v2.0");
        Assert.Equal(SourceType.Github, result.Type);
        Assert.Equal("getsentry", result.Owner);
        Assert.Equal("skills", result.Repo);
        Assert.Equal("v2.0", result.Ref);
        Assert.Equal("https://github.com/getsentry/skills.git", result.Url);
        Assert.Equal("git@github.com:getsentry/skills", result.CloneUrl);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParsesHttpsGithubUrlWithDottedRepoName()
    {
        var result = SkillResolver.ParseSource("https://github.com/vercel/next.js");
        Assert.Equal(SourceType.Github, result.Type);
        Assert.Equal("vercel", result.Owner);
        Assert.Equal("next.js", result.Repo);
        Assert.Equal("https://github.com/vercel/next.js.git", result.Url);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParsesHttpsGithubUrlWithDottedRepoNameAndGitSuffix()
    {
        var result = SkillResolver.ParseSource("https://github.com/vercel/next.js.git");
        Assert.Equal(SourceType.Github, result.Type);
        Assert.Equal("vercel", result.Owner);
        Assert.Equal("next.js", result.Repo);
        Assert.Equal("https://github.com/vercel/next.js.git", result.Url);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParsesHttpsGitlabUrl()
    {
        var result = SkillResolver.ParseSource("https://gitlab.com/group/repo");
        Assert.Equal(SourceType.Git, result.Type);
        Assert.Equal("group", result.Owner);
        Assert.Equal("repo", result.Repo);
        Assert.Equal("https://gitlab.com/group/repo.git", result.Url);
        Assert.Equal("https://gitlab.com/group/repo", result.CloneUrl);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParsesHttpsGitlabUrlWithSubgroup()
    {
        var result = SkillResolver.ParseSource("https://gitlab.com/group/subgroup/repo");
        Assert.Equal(SourceType.Git, result.Type);
        Assert.Equal("group/subgroup", result.Owner);
        Assert.Equal("repo", result.Repo);
        Assert.Equal("https://gitlab.com/group/subgroup/repo.git", result.Url);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ParsesSshGitlabUrlWithRef()
    {
        var result = SkillResolver.ParseSource("git@gitlab.com:group/repo@v2.0");
        Assert.Equal(SourceType.Git, result.Type);
        Assert.Equal("group", result.Owner);
        Assert.Equal("repo", result.Repo);
        Assert.Equal("v2.0", result.Ref);
        Assert.Equal("https://gitlab.com/group/repo.git", result.Url);
        Assert.Equal("git@gitlab.com:group/repo", result.CloneUrl);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task UpgradesHttpToHttpsInCloneUrl()
    {
        var result = SkillResolver.ParseSource("http://github.com/getsentry/skills");
        Assert.Equal(SourceType.Github, result.Type);
        Assert.Equal("https://github.com/getsentry/skills", result.CloneUrl);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task DoesNotSetCloneUrlForOwnerRepoShorthand()
    {
        var result = SkillResolver.ParseSource("getsentry/skills@v1.0");
        Assert.Null(result.CloneUrl);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task StripsRefContainingAtFromCloneUrlCorrectly()
    {
        var result = SkillResolver.ParseSource("git@github.com:org/repo@packages/foo@1.0.0");
        Assert.Equal(SourceType.Github, result.Type);
        Assert.Equal("org", result.Owner);
        Assert.Equal("repo", result.Repo);
        Assert.Equal("packages/foo@1.0.0", result.Ref);
        Assert.Equal("git@github.com:org/repo", result.CloneUrl);
        await Task.CompletedTask;
    }
}

public class NormalizeSourceTests
{
    [Fact]
    public async Task NormalizesOwnerRepoShorthandToItself()
    {
        Assert.Equal("getsentry/skills", SkillResolver.NormalizeSource("getsentry/skills"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task NormalizesGithubHttpsUrlToOwnerRepo()
    {
        Assert.Equal("getsentry/skills", SkillResolver.NormalizeSource("https://github.com/getsentry/skills"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task NormalizesGithubSshUrlToOwnerRepo()
    {
        Assert.Equal("getsentry/skills", SkillResolver.NormalizeSource("git@github.com:getsentry/skills.git"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task NormalizesGithubHttpsUrlWithGitSuffix()
    {
        Assert.Equal("getsentry/skills", SkillResolver.NormalizeSource("https://github.com/getsentry/skills.git"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task NormalizesGitlabHttpsUrlToGroupRepo()
    {
        Assert.Equal("group/repo", SkillResolver.NormalizeSource("https://gitlab.com/group/repo"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task NormalizesGitlabSshUrlToGroupRepo()
    {
        Assert.Equal("group/repo", SkillResolver.NormalizeSource("git@gitlab.com:group/repo.git"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ReturnsNonGithubSourcesUnchanged()
    {
        Assert.Equal("path:../my-skill", SkillResolver.NormalizeSource("path:../my-skill"));
        Assert.Equal("git:https://example.com/repo.git",
            SkillResolver.NormalizeSource("git:https://example.com/repo.git"));
        await Task.CompletedTask;
    }
}

public class SourcesMatchTests
{
    [Fact]
    public async Task MatchesIdenticalShorthand()
    {
        Assert.True(SkillResolver.SourcesMatch("getsentry/skills", "getsentry/skills"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task MatchesSshUrlWithShorthand()
    {
        Assert.True(SkillResolver.SourcesMatch("git@github.com:getsentry/skills.git", "getsentry/skills"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task MatchesHttpsUrlWithShorthand()
    {
        Assert.True(SkillResolver.SourcesMatch("https://github.com/getsentry/skills", "getsentry/skills"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task MatchesGitlabUrlWithShorthand()
    {
        Assert.True(SkillResolver.SourcesMatch("https://gitlab.com/getsentry/skills", "getsentry/skills"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task MatchesSshUrlWithHttpsUrl()
    {
        Assert.True(SkillResolver.SourcesMatch(
            "git@github.com:getsentry/skills.git",
            "https://github.com/getsentry/skills"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task MatchesGitlabSshUrlWithGitlabHttpsUrl()
    {
        Assert.True(SkillResolver.SourcesMatch(
            "git@gitlab.com:group/repo.git",
            "https://gitlab.com/group/repo"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task DoesNotMatchDifferentRepos()
    {
        Assert.False(SkillResolver.SourcesMatch("getsentry/skills", "getsentry/other"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task DoesNotMatchDifferentOwners()
    {
        Assert.False(SkillResolver.SourcesMatch("getsentry/skills", "anthropics/skills"));
        await Task.CompletedTask;
    }
}
