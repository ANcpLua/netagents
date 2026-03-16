namespace Qyl.Agents.Hosting;

using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Protocol;

/// <summary>
///     Extension methods for mapping an MCP server to ASP.NET Core endpoints.
///     Exposes JSON-RPC over HTTP POST, plus SKILL.md and llms.txt discovery endpoints.
/// </summary>
public static class McpEndpoints
{
    /// <summary>
    ///     Maps MCP server endpoints under the given <paramref name="pattern" />.
    ///     <list type="bullet">
    ///         <item><c>POST {pattern}</c> — handles JSON-RPC requests</item>
    ///         <item><c>GET {pattern}/skill.md</c> — serves SKILL.md content</item>
    ///         <item><c>GET {pattern}/llms.txt</c> — serves llms.txt content</item>
    ///     </list>
    /// </summary>
    public static IEndpointRouteBuilder MapMcpServer<TServer>(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/mcp") where TServer : class, IMcpServer, new()
    {
        var normalizedPattern = pattern.TrimEnd('/');

        endpoints.MapPost(normalizedPattern, async (HttpContext context) =>
        {
            var server = new TServer();
            var handler = new McpProtocolHandler<TServer>(server);

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
                // Notification — no response body per JSON-RPC spec
                context.Response.StatusCode = StatusCodes.Status204NoContent;
            }
        });

        endpoints.MapGet($"{normalizedPattern}/skill.md", (HttpContext context) =>
        {
            return Results.Text(TServer.SkillMd, "text/markdown");
        });

        endpoints.MapGet($"{normalizedPattern}/llms.txt", (HttpContext context) =>
        {
            return Results.Text(TServer.LlmsTxt, "text/plain");
        });

        return endpoints;
    }
}
