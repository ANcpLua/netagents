using Qyl.Agents.Generator.Models;

namespace Qyl.Agents.Generator.Generation;

internal static class MetadataEmitter
{
    public static void Emit(IndentedStringBuilder sb, ServerModel server)
    {
        // GetServerInfo
        sb.AppendLine("public static global::Qyl.Agents.McpServerInfo GetServerInfo()");
        using (sb.BeginBlock())
        {
            sb.AppendLine("return new global::Qyl.Agents.McpServerInfo");
            using (sb.BeginBlock())
            {
                sb.AppendLine($"Name = {Lit(server.ServerName)},");
                sb.AppendLine($"Description = {Lit(server.Description)},");
                sb.AppendLine(server.Version is not null
                    ? $"Version = {Lit(server.Version)},"
                    : "Version = null,");
            }

            sb.AppendLine(";");
        }

        sb.AppendLine();

        // GetToolInfos
        sb.AppendLine(
            "public static global::System.Collections.Generic.IReadOnlyList<global::Qyl.Agents.McpToolInfo> GetToolInfos()");
        using (sb.BeginBlock())
        {
            sb.AppendLine("return new global::Qyl.Agents.McpToolInfo[]");
            using (sb.BeginBlock())
            {
                foreach (var tool in server.Tools)
                {
                    sb.AppendLine("new global::Qyl.Agents.McpToolInfo");
                    using (sb.BeginBlock())
                    {
                        sb.AppendLine($"Name = {Lit(tool.ToolName)},");
                        sb.AppendLine($"Description = {Lit(tool.Description)},");
                        sb.AppendLine($"InputSchema = s_schema_{tool.MethodName}.ToArray(),");
                    }

                    sb.AppendLine(",");
                }
            }

            sb.AppendLine(";");
        }
    }

    private static string Lit(string? value) => EmitHelpers.Lit(value);
}