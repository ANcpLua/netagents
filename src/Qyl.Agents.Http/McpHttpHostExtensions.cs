namespace Qyl.Agents.Http;

using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Qyl.Agents.Protocol;

/// <summary>
///     Extension methods for mounting an MCP server on a <see cref="WebApplication" />.
///     Provides JSON-RPC endpoint and well-known discovery paths.
/// </summary>
public static class McpHttpHostExtensions
{
    /// <summary>
    ///     Maps an MCP server using the default parameterless constructor.
    ///     Mounts POST /mcp, GET /skill.md, GET /.well-known/skills/default/skill.md, and GET /llms.txt.
    /// </summary>
    public static WebApplication MapMcpServer<TServer>(this WebApplication app)
        where TServer : class, IMcpServer, new()
        => app.MapMcpServer(new TServer());

    /// <summary>
    ///     Maps an existing MCP server instance (for DI or factory patterns).
    ///     Mounts POST /mcp, GET /skill.md, GET /.well-known/skills/default/skill.md, and GET /llms.txt.
    /// </summary>
    public static WebApplication MapMcpServer<TServer>(this WebApplication app, TServer server)
        where TServer : class, IMcpServer
    {
        var handler = new McpProtocolHandler<TServer>(server);

        app.MapPost("/mcp", async (HttpContext ctx, CancellationToken ct) =>
        {
            JsonRpcRequest? request;
            try
            {
                request = await JsonSerializer.DeserializeAsync(
                    ctx.Request.Body,
                    JsonRpcJsonContext.Default.JsonRpcRequest,
                    ct);
            }
            catch (JsonException)
            {
                var parseError = new JsonRpcResponse
                {
                    Error = new JsonRpcError { Code = McpErrorCodes.ParseError, Message = "Invalid JSON" }
                };
                ctx.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(
                    ctx.Response.Body, parseError, JsonRpcJsonContext.Default.JsonRpcResponse, ct);
                return;
            }

            if (request is null)
            {
                var invalidError = new JsonRpcResponse
                {
                    Error = new JsonRpcError { Code = McpErrorCodes.InvalidRequest, Message = "Null request" }
                };
                ctx.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(
                    ctx.Response.Body, invalidError, JsonRpcJsonContext.Default.JsonRpcResponse, ct);
                return;
            }

            var response = await handler.HandleAsync(request, ct);

            if (response is not null)
            {
                ctx.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(
                    ctx.Response.Body, response, JsonRpcJsonContext.Default.JsonRpcResponse, ct);
            }
            else
            {
                ctx.Response.StatusCode = StatusCodes.Status204NoContent;
            }
        });

        app.MapGet("/skill.md", () => Results.Text(TServer.SkillMd, "text/markdown"));
        app.MapGet("/.well-known/skills/default/skill.md",
            () => Results.Text(TServer.SkillMd, "text/markdown"));
        app.MapGet("/llms.txt", () => Results.Text(TServer.LlmsTxt, "text/plain"));

        return app;
    }
}
