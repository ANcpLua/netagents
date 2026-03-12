using System.ComponentModel;
using System.Text.Json;
using NetAgents.Cli.Commands;
using NetAgents.Config;
using Qyl.Agents;

namespace NetAgents.Mcp;

[McpServer("netagents", Version = "1.2.0")]
public partial class NetAgentsMcpServer
{
    [Tool("list", Description = "List all declared skills and their installation status")]
    public async Task<string> ListAsync(
        [Description("Absolute path to the project root directory")] string projectRoot,
        CancellationToken ct)
    {
        var scope = ScopeResolver.ResolveScope(ScopeKind.Project, projectRoot);
        var result = await ListCommand.RunListAsync(new ListOptions(scope), ct).ConfigureAwait(false);
        return FormatJson(result);
    }

    private static string FormatJson<T>(T value) =>
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
}
