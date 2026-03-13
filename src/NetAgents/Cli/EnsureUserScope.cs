namespace NetAgents.Cli;

using Agents;
using Config;

public static class EnsureUserScope
{
    /// <summary>
    ///     Auto-bootstrap user scope if agents.toml doesn't exist yet.
    ///     Creates ~/.agents/agents.toml with sensible defaults so the user
    ///     can immediately start adding skills without a separate init step.
    /// </summary>
    public static async Task EnsureUserScopeBootstrappedAsync(ScopeRoot scope, CancellationToken ct = default)
    {
        if (scope.Scope != ScopeKind.User) return;
        if (File.Exists(scope.ConfigPath)) return;

        Directory.CreateDirectory(scope.SkillsDir);
        var content = ConfigWriter.GenerateDefaultConfig(new DefaultConfigOptions(
            AgentRegistry.AllAgentIds()));
        await File.WriteAllTextAsync(scope.ConfigPath, content, ct).ConfigureAwait(false);
        Console.Error.WriteLine("Initialized ~/.agents/agents.toml");
    }
}
