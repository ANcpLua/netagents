namespace NetAgents.Agents;

using System.Runtime.InteropServices;

public sealed record UserMcpTarget(string FilePath, bool Shared);

public static class AgentPaths
{
    public static UserMcpTarget GetUserMcpTarget(string agentId)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return agentId switch
        {
            "claude" => new UserMcpTarget(Path.Combine(home, ".claude.json"), true),
            "cursor" => new UserMcpTarget(Path.Combine(home, ".cursor", "mcp.json"), false),
            "codex" => new UserMcpTarget(Path.Combine(home, ".codex", "config.toml"), true),
            "vscode" => new UserMcpTarget(VsCodeMcpPath(home), false),
            "opencode" => new UserMcpTarget(Path.Combine(home, ".config", "opencode", "opencode.json"), true),
            _ => throw new ArgumentException($"Unknown agent for user-scope MCP: {agentId}", nameof(agentId))
        };
    }

    public static McpTargetResolver UserMcpResolver()
    {
        return (agentId, _) =>
        {
            var target = GetUserMcpTarget(agentId);
            return new McpResolvedTarget(target.FilePath, target.Shared);
        };
    }

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
