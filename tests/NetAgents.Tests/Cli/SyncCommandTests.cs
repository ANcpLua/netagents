namespace NetAgents.Tests.Cli;

using AwesomeAssertions;
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

        result.Adopted.Should().BeEquivalentTo(["orphan"]);
        result.Issues.Should().BeEmpty();

        var config = await ConfigLoader.LoadAsync(scope.ConfigPath, CT);
        var skill = config.Skills.OfType<RegularSkillDependency>().FirstOrDefault(s => s.Name == "orphan");
        skill.Should().NotBeNull();
        skill!.Source.Should().Be("path:.agents/skills/orphan");

        var lockfile = await LockfileLoader.LoadAsync(scope.LockPath, CT);
        lockfile.Should().NotBeNull();
        lockfile!.Skills.ContainsKey("orphan").Should().BeTrue();
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

        result.Adopted.Count.Should().Be(2);
        result.Adopted.Should().Contain("alpha");
        result.Adopted.Should().Contain("beta");
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
        missing.Should().ContainSingle();
        missing[0].Name.Should().Be("pdf");
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

        result.Issues.Should().BeEmpty();
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

        result.SymlinksRepaired.Should().Be(1);
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

        result.GitignoreUpdated.Should().BeTrue();
        var gitignore = await File.ReadAllTextAsync(Path.Combine(tmp.Path, ".agents", ".gitignore"), CT);
        gitignore.Should().Contain("/skills/pdf/");
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

        (result.McpRepaired > 0).Should().BeTrue();
        File.Exists(Path.Combine(tmp.Path, ".mcp.json")).Should().BeTrue();
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

        (result.HooksRepaired > 0).Should().BeTrue();
        File.Exists(Path.Combine(tmp.Path, ".claude", "settings.json")).Should().BeTrue();
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

        result.HooksRepaired.Should().Be(0);
        result.Issues.Should().NotContain(i => i.Type == "hooks");
    }
}
