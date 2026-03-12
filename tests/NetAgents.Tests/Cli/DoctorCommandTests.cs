using NetAgents.Cli.Commands;
using Xunit;

namespace NetAgents.Tests.Cli;

file sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
    public TempDir() => Directory.CreateDirectory(Path);
    public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); }
}

public sealed class DoctorCommandTests
{
    private static CancellationToken CT => TestContext.Current.CancellationToken;

    private static string SetupProject(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, ".agents", "skills"));
        return root;
    }

    [Fact]
    public async Task ReportsError_WhenAgentsTomlMissing()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var result = await DoctorCommand.RunDoctorAsync(new DoctorOptions(scope), CT);

        var check = result.Checks.First(c => c.Name == "agents.toml");
        Assert.Equal("error", check.Status);
    }

    [Fact]
    public async Task AllChecksPass_ForCleanProject()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"), "version = 1\n");
        File.WriteAllText(Path.Combine(tmp.Path, ".gitignore"), "agents.lock\n.agents/.gitignore\n");
        File.WriteAllText(Path.Combine(tmp.Path, ".agents", ".gitignore"), "# managed\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var result = await DoctorCommand.RunDoctorAsync(new DoctorOptions(scope), CT);

        Assert.True(result.Checks.All(c => c.Status == "ok"));
    }

    [Fact]
    public async Task DetectsLegacyPinField()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"), "version = 1\npin = true\n");
        File.WriteAllText(Path.Combine(tmp.Path, ".gitignore"), "agents.lock\n.agents/.gitignore\n");
        File.WriteAllText(Path.Combine(tmp.Path, ".agents", ".gitignore"), "# managed\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var result = await DoctorCommand.RunDoctorAsync(new DoctorOptions(scope), CT);

        var check = result.Checks.First(c => c.Name == "legacy pin field");
        Assert.Equal("warn", check.Status);
    }

    [Fact]
    public async Task DetectsLegacyGitignoreField()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"), "version = 1\ngitignore = true\n");
        File.WriteAllText(Path.Combine(tmp.Path, ".gitignore"), "agents.lock\n.agents/.gitignore\n");
        File.WriteAllText(Path.Combine(tmp.Path, ".agents", ".gitignore"), "# managed\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var result = await DoctorCommand.RunDoctorAsync(new DoctorOptions(scope), CT);

        var check = result.Checks.First(c => c.Name == "legacy gitignore field");
        Assert.Equal("warn", check.Status);
    }

    [Fact]
    public async Task DetectsLegacyLockfileFields()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"), "version = 1\n");
        File.WriteAllText(Path.Combine(tmp.Path, "agents.lock"),
            "version = 1\n\n[skills.pdf]\nsource = \"org/repo\"\nresolved_url = \"https://github.com/org/repo.git\"\nresolved_path = \"pdf\"\ncommit = \"abc123\"\nintegrity = \"sha256-xxx\"\n");
        File.WriteAllText(Path.Combine(tmp.Path, ".gitignore"), "agents.lock\n.agents/.gitignore\n");
        File.WriteAllText(Path.Combine(tmp.Path, ".agents", ".gitignore"), "# managed\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var result = await DoctorCommand.RunDoctorAsync(new DoctorOptions(scope), CT);

        var check = result.Checks.First(c => c.Name == "legacy lockfile fields");
        Assert.Equal("warn", check.Status);
    }

    [Fact]
    public async Task DetectsMissingRootGitignoreEntries()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"), "version = 1\n");
        File.WriteAllText(Path.Combine(tmp.Path, ".agents", ".gitignore"), "# managed\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var result = await DoctorCommand.RunDoctorAsync(new DoctorOptions(scope), CT);

        var check = result.Checks.First(c => c.Name == "root .gitignore");
        Assert.Equal("error", check.Status);
    }

    [Fact]
    public async Task DetectsMissingAgentsGitignore()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"), "version = 1\n");
        File.WriteAllText(Path.Combine(tmp.Path, ".gitignore"), "agents.lock\n.agents/.gitignore\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var result = await DoctorCommand.RunDoctorAsync(new DoctorOptions(scope), CT);

        var check = result.Checks.First(c => c.Name == ".agents/.gitignore");
        Assert.Equal("warn", check.Status);
    }

    [Fact]
    public async Task DetectsMissingSkills()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"),
            "version = 1\n\n[[skills]]\nname = \"pdf\"\nsource = \"org/repo\"\n");
        File.WriteAllText(Path.Combine(tmp.Path, ".gitignore"), "agents.lock\n.agents/.gitignore\n");
        File.WriteAllText(Path.Combine(tmp.Path, ".agents", ".gitignore"), "# managed\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var result = await DoctorCommand.RunDoctorAsync(new DoctorOptions(scope), CT);

        var check = result.Checks.First(c => c.Name == "installed skills");
        Assert.Equal("error", check.Status);
        Assert.Contains("pdf", check.Message);
    }

    [Fact]
    public async Task FixesMissingRootGitignore()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"), "version = 1\n");
        File.WriteAllText(Path.Combine(tmp.Path, ".agents", ".gitignore"), "# managed\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var result = await DoctorCommand.RunDoctorAsync(new DoctorOptions(scope, Fix: true), CT);

        Assert.True(result.Fixed > 0);
        var gitignore = await File.ReadAllTextAsync(Path.Combine(tmp.Path, ".gitignore"), CT);
        Assert.Contains("agents.lock", gitignore);
        Assert.Contains(".agents/.gitignore", gitignore);
    }

    [Fact]
    public async Task FixesLegacyPinField()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"), "version = 1\npin = true\n# pinning notes\n");
        File.WriteAllText(Path.Combine(tmp.Path, ".gitignore"), "agents.lock\n.agents/.gitignore\n");
        File.WriteAllText(Path.Combine(tmp.Path, ".agents", ".gitignore"), "# managed\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await DoctorCommand.RunDoctorAsync(new DoctorOptions(scope, Fix: true), CT);

        var content = await File.ReadAllTextAsync(Path.Combine(tmp.Path, "agents.toml"), CT);
        Assert.DoesNotMatch(@"^\s*pin\s*=", content);
        Assert.Contains("# pinning notes", content);
    }

    [Fact]
    public async Task FixesLegacyGitignoreField()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"), "version = 1\ngitignore = true\n");
        File.WriteAllText(Path.Combine(tmp.Path, ".gitignore"), "agents.lock\n.agents/.gitignore\n");
        File.WriteAllText(Path.Combine(tmp.Path, ".agents", ".gitignore"), "# managed\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await DoctorCommand.RunDoctorAsync(new DoctorOptions(scope, Fix: true), CT);

        var content = await File.ReadAllTextAsync(Path.Combine(tmp.Path, "agents.toml"), CT);
        Assert.DoesNotContain("gitignore", content);
    }

    [Fact]
    public async Task CreatesMissingAgentsGitignore()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"), "version = 1\n");
        File.WriteAllText(Path.Combine(tmp.Path, ".gitignore"), "agents.lock\n.agents/.gitignore\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await DoctorCommand.RunDoctorAsync(new DoctorOptions(scope, Fix: true), CT);

        Assert.True(File.Exists(Path.Combine(tmp.Path, ".agents", ".gitignore")));
    }
}
