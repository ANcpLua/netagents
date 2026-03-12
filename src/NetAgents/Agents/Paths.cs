using System.Runtime.InteropServices;

namespace NetAgents.Agents;

public sealed record UserMcpTarget(string FilePath, bool Shared);

public static class AgentPaths
{
    public static UserMcpTarget GetUserMcpTarget(string agentId)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return agentId switch
        {
            "claude" => new UserMcpTarget(Path.Combine(home, ".claude.json"), Shared: true),
            "cursor" => new UserMcpTarget(Path.Combine(home, ".cursor", "mcp.json"), Shared: false),
            "codex" => new UserMcpTarget(Path.Combine(home, ".codex", "config.toml"), Shared: true),
            "vscode" => new UserMcpTarget(VsCodeMcpPath(home), Shared: false),
            "opencode" => new UserMcpTarget(Path.Combine(home, ".config", "opencode", "opencode.json"), Shared: true),
            _ => throw new ArgumentException($"Unknown agent for user-scope MCP: {agentId}", nameof(agentId)),
        };
    }

    public static McpTargetResolver UserMcpResolver() =>
        (agentId, _) =>
        {
            var target = GetUserMcpTarget(agentId);
            return new McpResolvedTarget(target.FilePath, target.Shared);
        };

    private static string VsCodeMcpPath(string home)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return Path.Combine(home, "Library", "Application Support", "Code", "User", "mcp.json");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var appData = Environment.GetEnvironmentVariable("APPDATA")
                          ?? Path.Combine(home, "AppData", "Roaming");
            return Path.Combine(appData, "Code", "User", "mcp.json");
        }

        return Path.Combine(home, ".config", "Code", "User", "mcp.json");
    }
}
