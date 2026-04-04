namespace NetAgents.Tests.Cli;

using AwesomeAssertions;
using NetAgents.Cli.Commands;
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
        check.Status.Should().Be("error");
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

        result.Checks.All(c => c.Status == "ok").Should().BeTrue();
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
        check.Status.Should().Be("warn");
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
        check.Status.Should().Be("warn");
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
        check.Status.Should().Be("warn");
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
        check.Status.Should().Be("error");
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
        check.Status.Should().Be("warn");
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
        check.Status.Should().Be("error");
        check.Message.Should().Contain("pdf");
    }

    [Fact]
    public async Task FixesMissingRootGitignore()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"), "version = 1\n");
        File.WriteAllText(Path.Combine(tmp.Path, ".agents", ".gitignore"), "# managed\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        var result = await DoctorCommand.RunDoctorAsync(new DoctorOptions(scope, true), CT);

        (result.Fixed > 0).Should().BeTrue();
        var gitignore = await File.ReadAllTextAsync(Path.Combine(tmp.Path, ".gitignore"), CT);
        gitignore.Should().Contain("agents.lock");
        gitignore.Should().Contain(".agents/.gitignore");
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

        await DoctorCommand.RunDoctorAsync(new DoctorOptions(scope, true), CT);

        var content = await File.ReadAllTextAsync(Path.Combine(tmp.Path, "agents.toml"), CT);
        Assert.DoesNotMatch(@"^\s*pin\s*=", content);
        content.Should().Contain("# pinning notes");
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

        await DoctorCommand.RunDoctorAsync(new DoctorOptions(scope, true), CT);

        var content = await File.ReadAllTextAsync(Path.Combine(tmp.Path, "agents.toml"), CT);
        content.Should().NotContain("gitignore");
    }

    [Fact]
    public async Task CreatesMissingAgentsGitignore()
    {
        using var tmp = new TempDir();
        SetupProject(tmp.Path);
        File.WriteAllText(Path.Combine(tmp.Path, "agents.toml"), "version = 1\n");
        File.WriteAllText(Path.Combine(tmp.Path, ".gitignore"), "agents.lock\n.agents/.gitignore\n");
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, tmp.Path);

        await DoctorCommand.RunDoctorAsync(new DoctorOptions(scope, true), CT);

        File.Exists(Path.Combine(tmp.Path, ".agents", ".gitignore")).Should().BeTrue();
    }
}
