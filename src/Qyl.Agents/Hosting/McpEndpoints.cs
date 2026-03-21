namespace Qyl.Agents.Hosting;

using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Protocol;
using Tasks;

/// <summary>
///     Extension methods for mapping an MCP server to ASP.NET Core endpoints.
///     Exposes JSON-RPC over HTTP POST, plus skill.md, llms.txt, and well-known discovery endpoints.
/// </summary>
public static class McpEndpoints
{
    /// <summary>
    ///     Maps MCP server endpoints using the default parameterless constructor.
    /// </summary>
    public static IEndpointRouteBuilder MapMcpServer<TServer>(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/mcp") where TServer : class, IMcpServer, new()
        => endpoints.MapMcpServer(new TServer(), pattern);

    /// <summary>
    ///     Maps MCP server endpoints using the default parameterless constructor, with task support.
    /// </summary>
    public static IEndpointRouteBuilder MapMcpServer<TServer>(
        this IEndpointRouteBuilder endpoints,
        IMcpTaskStore taskStore,
        string pattern = "/mcp") where TServer : class, IMcpServer, new()
        => endpoints.MapMcpServer(new TServer(), taskStore, pattern);

    /// <summary>
    ///     Maps an existing MCP server instance (for DI or factory patterns).
    ///     <list type="bullet">
    ///         <item><c>POST {pattern}</c> — handles JSON-RPC requests</item>
    ///         <item><c>GET {pattern}/skill.md</c> — serves SKILL.md content</item>
    ///         <item><c>GET {pattern}/llms.txt</c> — serves llms.txt content</item>
    ///         <item><c>GET /.well-known/skills/default/skill.md</c> — well-known discovery</item>
    ///     </list>
    /// </summary>
    public static IEndpointRouteBuilder MapMcpServer<TServer>(
        this IEndpointRouteBuilder endpoints,
        TServer server,
        string pattern = "/mcp") where TServer : class, IMcpServer
        => endpoints.MapMcpServerCore(server, null, pattern);

    /// <summary>
    ///     Maps an existing MCP server instance with task store for long-running tool support.
    /// </summary>
    public static IEndpointRouteBuilder MapMcpServer<TServer>(
        this IEndpointRouteBuilder endpoints,
        TServer server,
        IMcpTaskStore taskStore,
        string pattern = "/mcp") where TServer : class, IMcpServer
        => endpoints.MapMcpServerCore(server, taskStore, pattern);

    private static IEndpointRouteBuilder MapMcpServerCore<TServer>(
        this IEndpointRouteBuilder endpoints,
        TServer server,
        IMcpTaskStore? taskStore,
        string pattern) where TServer : class, IMcpServer
    {
        var handler = new McpProtocolHandler<TServer>(server, taskStore);
        var normalizedPattern = pattern.TrimEnd('/');

        endpoints.MapPost(normalizedPattern, async (HttpContext context) =>
        {
            JsonRpcRequest? request;
            try
            {
                request = await JsonSerializer.DeserializeAsync(
                    context.Request.Body,
                    JsonRpcJsonContext.Default.JsonRpcRequest,
                    context.RequestAborted);
            }
            catch (JsonException)
            {
                var parseError = new JsonRpcResponse
                {
                    Error = new JsonRpcError { Code = McpErrorCodes.ParseError, Message = "Invalid JSON" }
                };
                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(
                    context.Response.Body,
                    parseError,
                    JsonRpcJsonContext.Default.JsonRpcResponse,
                    context.RequestAborted);
                return;
            }

            if (request is null)
            {
                var invalidError = new JsonRpcResponse
                {
                    Error = new JsonRpcError { Code = McpErrorCodes.InvalidRequest, Message = "Null request" }
                };
                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(
                    context.Response.Body,
                    invalidError,
                    JsonRpcJsonContext.Default.JsonRpcResponse,
                    context.RequestAborted);
                return;
            }

            var response = await handler.HandleAsync(request, context.RequestAborted);

            if (response is not null)
            {
                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(
                    context.Response.Body,
                    response,
                    JsonRpcJsonContext.Default.JsonRpcResponse,
                    context.RequestAborted);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
            }
        });

        endpoints.MapGet($"{normalizedPattern}/skill.md",
            () => Results.Text(TServer.SkillMd, "text/markdown"));

        endpoints.MapGet($"{normalizedPattern}/llms.txt",
            () => Results.Text(TServer.LlmsTxt, "text/plain"));

        endpoints.MapGet("/.well-known/skills/default/skill.md",
            () => Results.Text(TServer.SkillMd, "text/markdown"));

        return endpoints;
    }
}
