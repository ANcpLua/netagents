namespace NetAgents.Tests.Cli;

using NetAgents.Cli.Commands;
using NetAgents.Lockfile;
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

public sealed class ListCommandTests
{
    private static CancellationToken CT => TestContext.Current.CancellationToken;

    private static string SetupProject(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, ".agents", "skills"));
        return root;
    }

    [Fact]
    public async Task ReturnsEmpty_WhenNoSkillsDeclared()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"), "version = 1\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var results = await ListCommand.RunListAsync(new ListOptions(scope), CT);

        Assert.Empty(results);
    }

    [Fact]
    public async Task ReportsMissing_WhenNotInstalled()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"),
            "version = 1\n\n[[skills]]\nname = \"pdf\"\nsource = \"org/repo\"\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var results = await ListCommand.RunListAsync(new ListOptions(scope), CT);

        Assert.Single(results);
        Assert.Equal("missing", results[0].Status);
    }

    [Fact]
    public async Task ReportsUnlocked_WhenNoLockfile()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"),
            "version = 1\n\n[[skills]]\nname = \"pdf\"\nsource = \"org/repo\"\n");
        var skillDir = Path.Combine(tmp.Path, ".agents", "skills", "pdf");
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "---\nname: pdf\n---\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var results = await ListCommand.RunListAsync(new ListOptions(scope), CT);

        Assert.Single(results);
        Assert.Equal("unlocked", results[0].Status);
    }

    [Fact]
    public async Task ReportsOk_WhenInstalledAndLocked()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"),
            "version = 1\n\n[[skills]]\nname = \"pdf\"\nsource = \"org/repo\"\n");
        var skillDir = Path.Combine(tmp.Path, ".agents", "skills", "pdf");
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "---\nname: pdf\n---\n");

        var lockData = new LockfileData(1, new Dictionary<string, LockedSkill>
        {
            ["pdf"] = new LockedGitSkill("org/repo", "https://github.com/org/repo.git", "pdf", null)
        });
        await LockfileWriter.WriteAsync(Path.Combine(tmp.Path, "agents.lock"), lockData, CT);
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var results = await ListCommand.RunListAsync(new ListOptions(scope), CT);

        Assert.Single(results);
        Assert.Equal("ok", results[0].Status);
    }

    [Fact]
    public async Task SortsResultsByName()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"),
            "version = 1\n\n[[skills]]\nname = \"z-skill\"\nsource = \"org/z\"\n\n[[skills]]\nname = \"a-skill\"\nsource = \"org/a\"\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var results = await ListCommand.RunListAsync(new ListOptions(scope), CT);

        Assert.Equal("a-skill", results[0].Name);
        Assert.Equal("z-skill", results[1].Name);
    }

    [Fact]
    public async Task ListsWildcardExpandedSkills_FromLockfile()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"),
            "version = 1\n\n[[skills]]\nname = \"*\"\nsource = \"org/repo\"\n");

        foreach (var name in new[] { "pdf", "review" })
        {
            var dir = Path.Combine(tmp.Path, ".agents", "skills", name);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "SKILL.md"), $"---\nname: {name}\n---\n");
        }

        var lockData = new LockfileData(1, new Dictionary<string, LockedSkill>
        {
            ["pdf"] = new LockedGitSkill("org/repo", "https://github.com/org/repo.git", "pdf", null),
            ["review"] = new LockedGitSkill("org/repo", "https://github.com/org/repo.git", "skills/review", null)
        });
        await LockfileWriter.WriteAsync(Path.Combine(tmp.Path, "agents.lock"), lockData, CT);
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var results = await ListCommand.RunListAsync(new ListOptions(scope), CT);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("org/repo", r.Wildcard));
    }

    [Fact]
    public async Task WildcardExcludeIsRespected()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"),
            "version = 1\n\n[[skills]]\nname = \"*\"\nsource = \"org/repo\"\nexclude = [\"review\"]\n");

        var pdfDir = Path.Combine(tmp.Path, ".agents", "skills", "pdf");
        Directory.CreateDirectory(pdfDir);
        File.WriteAllText(Path.Combine(pdfDir, "SKILL.md"), "---\nname: pdf\n---\n");

        var lockData = new LockfileData(1, new Dictionary<string, LockedSkill>
        {
            ["pdf"] = new LockedGitSkill("org/repo", "https://github.com/org/repo.git", "pdf", null),
            ["review"] = new LockedGitSkill("org/repo", "https://github.com/org/repo.git", "skills/review", null)
        });
        await LockfileWriter.WriteAsync(Path.Combine(tmp.Path, "agents.lock"), lockData, CT);
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var results = await ListCommand.RunListAsync(new ListOptions(scope), CT);

        Assert.Single(results);
        Assert.Equal("pdf", results[0].Name);
    }
}
