using System.Text.Json;
using Qyl.Agents.Protocol;

namespace Qyl.Agents.Hosting;

/// <summary>
///     Hosts an MCP server over stdio (JSON-RPC over stdin/stdout).
///     Primary transport for Claude Code, Cursor, and other CLI-based AI agents.
/// </summary>
public static class McpHost
{
    /// <summary>
    ///     Runs the MCP server, reading JSON-RPC requests from stdin and writing responses to stdout.
    ///     Blocks until stdin is closed or cancellation is requested.
    /// </summary>
    public static async Task RunStdioAsync<TServer>(
        TServer server,
        CancellationToken ct = default) where TServer : class, IMcpServer
    {
        var handler = new McpProtocolHandler<TServer>(server);
        var reader = Console.OpenStandardInput();
        var writer = Console.OpenStandardOutput();

        using var streamReader = new StreamReader(reader);
        using var streamWriter = new StreamWriter(writer) { AutoFlush = true };

        while (!ct.IsCancellationRequested)
        {
            var line = await streamReader.ReadLineAsync(ct);
            if (line is null) break; // stdin closed

            if (string.IsNullOrWhiteSpace(line)) continue;

            JsonRpcRequest? request;
            try
            {
                request = JsonSerializer.Deserialize(line, JsonRpcJsonContext.Default.JsonRpcRequest);
            }
            catch (JsonException)
            {
                var parseError = new JsonRpcResponse
                {
                    Error = new JsonRpcError { Code = McpErrorCodes.ParseError, Message = "Invalid JSON" }
                };
                await WriteResponseAsync(streamWriter, parseError, ct);
                continue;
            }

            if (request is null)
            {
                var invalidError = new JsonRpcResponse
                {
                    Error = new JsonRpcError { Code = McpErrorCodes.InvalidRequest, Message = "Null request" }
                };
                await WriteResponseAsync(streamWriter, invalidError, ct);
                continue;
            }

            var response = await handler.HandleAsync(request, ct);

            if (response is not null)
                await WriteResponseAsync(streamWriter, response, ct);
        }
    }

    /// <summary>
    ///     Writes the generated SKILL.md to disk for dotagents distribution.
    ///     Call from a build target or CLI tool to emit the skill manifest.
    /// </summary>
    /// <param name="outputPath">Path to write the SKILL.md file.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task WriteSkillMdAsync<TServer>(
        string outputPath,
        CancellationToken ct = default) where TServer : class, IMcpServer
    {
        var content = TServer.SkillMd;
        await File.WriteAllTextAsync(outputPath, content, ct);
    }

    private static async Task WriteResponseAsync(
        StreamWriter writer,
        JsonRpcResponse response,
        CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(response, JsonRpcJsonContext.Default.JsonRpcResponse);
        await writer.WriteLineAsync(json.AsMemory(), ct);
    }
}