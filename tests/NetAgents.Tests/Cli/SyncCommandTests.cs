namespace NetAgents.Tests.Cli;

using NetAgents.Cli.Commands;
using NetAgents.Config;
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

public sealed class SyncCommandTests
{
    private static CancellationToken CT => TestContext.Current.CancellationToken;

    private static string SetupProject(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, ".agents", "skills"));
        return root;
    }

    [Fact]
    public async Task AdoptsOrphanedSkill()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"), "version = 1\n");
        var orphanDir = Path.Combine(tmp.Path, ".agents", "skills", "orphan");
        Directory.CreateDirectory(orphanDir);
        File.WriteAllText(Path.Combine(orphanDir, "SKILL.md"), "---\nname: orphan\n---\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var result = await SyncCommand.RunSyncAsync(new SyncOptions(scope), CT);

        Assert.Equal(["orphan"], result.Adopted);
        Assert.Empty(result.Issues);

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        var skill = config.Skills.OfType<RegularSkillDependency>().FirstOrDefault(s => s.Name == "orphan");
        Assert.NotNull(skill);
        Assert.Equal("path:.agents/skills/orphan", skill.Source);

        var lockfile = await LockfileLoader.LoadAsync(scope.LockPath, CT);
        Assert.NotNull(lockfile);
        Assert.True(lockfile.Skills.ContainsKey("orphan"));
    }

    [Fact]
    public async Task AdoptsMultipleOrphans()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"), "version = 1\n");
        foreach (var name in new[] { "alpha", "beta" })
        {
            var dir = Path.Combine(tmp.Path, ".agents", "skills", name);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "SKILL.md"), $"---\nname: {name}\n---\n");
        }

        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var result = await SyncCommand.RunSyncAsync(new SyncOptions(scope), CT);

        Assert.Equal(2, result.Adopted.Count);
        Assert.Contains("alpha", result.Adopted);
        Assert.Contains("beta", result.Adopted);
    }

    [Fact]
    public async Task DetectsMissingSkills()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"),
            "version = 1\n\n[[skills]]\nname = \"pdf\"\nsource = \"org/repo\"\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var result = await SyncCommand.RunSyncAsync(new SyncOptions(scope), CT);

        var missing = result.Issues.Where(i => i.Type == "missing").ToList();
        Assert.Single(missing);
        Assert.Equal("pdf", missing[0].Name);
    }

    [Fact]
    public async Task ReportsNoIssues_WhenInSync()
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

        var result = await SyncCommand.RunSyncAsync(new SyncOptions(scope), CT);

        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task RepairsBrokenSymlinks()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"),
            "version = 1\n\n[symlinks]\ntargets = [\".claude\"]\n");
        Directory.CreateDirectory(Path.Combine(tmp.Path, ".claude"));
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var result = await SyncCommand.RunSyncAsync(new SyncOptions(scope), CT);

        Assert.Equal(1, result.SymlinksRepaired);
    }

    [Fact]
    public async Task RegeneratesGitignore()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"),
            "version = 1\n\n[[skills]]\nname = \"pdf\"\nsource = \"org/repo\"\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var result = await SyncCommand.RunSyncAsync(new SyncOptions(scope), CT);

        Assert.True(result.GitignoreUpdated);
        var gitignore = await File.ReadAllTextAsync(Path.Combine(tmp.Path, ".agents", ".gitignore"), CT);
        Assert.Contains("/skills/pdf/", gitignore);
    }

    [Fact]
    public async Task RepairsMissingMcpConfigs()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"),
            "version = 1\nagents = [\"claude\"]\n\n[[mcp]]\nname = \"github\"\ncommand = \"npx\"\nargs = [\"-y\", \"@mcp/server-github\"]\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var result = await SyncCommand.RunSyncAsync(new SyncOptions(scope), CT);

        Assert.True(result.McpRepaired > 0);
        Assert.True(File.Exists(Path.Combine(tmp.Path, ".mcp.json")));
    }

    [Fact]
    public async Task RepairsMissingHookConfigs()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"),
            "version = 1\nagents = [\"claude\"]\n\n[[hooks]]\nevent = \"PreToolUse\"\nmatcher = \"Bash\"\ncommand = \".agents/hooks/block-rm.sh\"\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var result = await SyncCommand.RunSyncAsync(new SyncOptions(scope), CT);

        Assert.True(result.HooksRepaired > 0);
        Assert.True(File.Exists(Path.Combine(tmp.Path, ".claude", "settings.json")));
    }

    [Fact]
    public async Task NoHookIssues_WhenConfigsPresent()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"),
            "version = 1\nagents = [\"claude\"]\n\n[[hooks]]\nevent = \"Stop\"\ncommand = \"check.sh\"\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await SyncCommand.RunSyncAsync(new SyncOptions(scope), CT);
        var result = await SyncCommand.RunSyncAsync(new SyncOptions(scope), CT);

        Assert.Equal(0, result.HooksRepaired);
        Assert.DoesNotContain(result.Issues, i => i.Type == "hooks");
    }
}
