namespace NetAgents.Tests.Mcp;

using System.Diagnostics;
using System.Text.Json;
using AwesomeAssertions;
using NetAgents.Mcp;
using Xunit;

[Collection("SerialGit")]
public sealed class StdioIntegrationTests
{
    private static CancellationToken CT => TestContext.Current.CancellationToken;

    private static Process StartServer()
    {
        var dllPath = typeof(NetAgentsMcpServer).Assembly.Location;
        var p = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList = { dllPath, "mcp", "serve" },
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        p.Start();
        return p;
    }

    private static async Task<string> HandshakeAsync(StreamWriter writer, StreamReader reader, CancellationToken ct)
    {
        await writer.WriteLineAsync(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0", id = 1, method = "initialize",
            @params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "test", version = "1.0.0" }
            }
        }).AsMemory(), ct);
        await writer.FlushAsync(ct);
        var initResponse = await reader.ReadLineAsync(ct);

        await writer.WriteLineAsync(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0", method = "notifications/initialized"
        }).AsMemory(), ct);
        await writer.FlushAsync(ct);

        return initResponse!;
    }

    [Fact]
    public async Task Handshake_And_ToolsCall_ReturnsValidJsonRpc()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var project = Path.Combine(tmp, "project");
        Directory.CreateDirectory(Path.Combine(project, ".agents", "skills"));
        File.WriteAllText(Path.Combine(project, "agents.toml"), "version = 1\n");

        using var process = StartServer();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CT);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        var initResponse = await HandshakeAsync(process.StandardInput, process.StandardOutput, cts.Token);
        var initDoc = JsonDocument.Parse(initResponse);
        initDoc.RootElement.GetProperty("jsonrpc").GetString().Should().Be("2.0");
        initDoc.RootElement.GetProperty("result").TryGetProperty("serverInfo", out _).Should().BeTrue();

        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0", id = 2, method = "tools/call",
            @params = new { name = "list", arguments = new { projectRoot = project } }
        }).AsMemory(), cts.Token);
        await process.StandardInput.FlushAsync(cts.Token);

        var toolResponse = await process.StandardOutput.ReadLineAsync(cts.Token);
        var toolDoc = JsonDocument.Parse(toolResponse!);
        toolDoc.RootElement.GetProperty("id").GetInt32().Should().Be(2);
        toolDoc.RootElement.GetProperty("result").TryGetProperty("content", out _).Should().BeTrue();

        process.StandardInput.Close();
        await process.WaitForExitAsync(cts.Token);

        if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
    }

    [Fact]
    public async Task ToolsCall_WithInvalidProjectRoot_ReturnsIsError()
    {
        using var process = StartServer();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CT);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        await HandshakeAsync(process.StandardInput, process.StandardOutput, cts.Token);

        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0", id = 2, method = "tools/call",
            @params = new { name = "install", arguments = new { projectRoot = "/nonexistent-path" } }
        }).AsMemory(), cts.Token);
        await process.StandardInput.FlushAsync(cts.Token);

        var response = await process.StandardOutput.ReadLineAsync(cts.Token);
        var doc = JsonDocument.Parse(response!);
        var result = doc.RootElement.GetProperty("result");
        result.GetProperty("isError").GetBoolean().Should().BeTrue();

        process.StandardInput.Close();
        await process.WaitForExitAsync(cts.Token);
    }
}
