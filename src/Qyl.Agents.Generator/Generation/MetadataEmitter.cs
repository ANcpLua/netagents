namespace Qyl.Agents.Generator.Generation;

using Models;

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
                        if (tool.ReadOnly != ToolHintValue.Unset)
                            sb.AppendLine(
                                $"ReadOnlyHint = {HintBool(tool.ReadOnly)},");
                        if (tool.Destructive != ToolHintValue.Unset)
                            sb.AppendLine(
                                $"DestructiveHint = {HintBool(tool.Destructive)},");
                        if (tool.Idempotent != ToolHintValue.Unset)
                            sb.AppendLine(
                                $"IdempotentHint = {HintBool(tool.Idempotent)},");
                        if (tool.OpenWorld != ToolHintValue.Unset)
                            sb.AppendLine(
                                $"OpenWorldHint = {HintBool(tool.OpenWorld)},");
                        if (tool.TaskSupport != ToolTaskSupportValue.Unset)
                            sb.AppendLine(
                                $"TaskSupport = global::Qyl.Agents.ToolTaskSupport.{tool.TaskSupport},");
                    }

                    sb.AppendLine(",");
                }
            }

            sb.AppendLine(";");
        }

        sb.AppendLine();

        // GetResourceInfos
        sb.AppendLine(
            "public static global::System.Collections.Generic.IReadOnlyList<global::Qyl.Agents.McpResourceInfo> GetResourceInfos()");
        using (sb.BeginBlock())
        {
            if (server.Resources.IsEmpty)
            {
                sb.AppendLine(
                    "return global::System.Array.Empty<global::Qyl.Agents.McpResourceInfo>();");
            }
            else
            {
                sb.AppendLine("return new global::Qyl.Agents.McpResourceInfo[]");
                using (sb.BeginBlock())
                {
                    foreach (var resource in server.Resources)
                    {
                        sb.AppendLine("new global::Qyl.Agents.McpResourceInfo");
                        using (sb.BeginBlock())
                        {
                            sb.AppendLine($"Uri = {Lit(resource.Uri)},");
                            if (resource.Description is not null)
                                sb.AppendLine($"Description = {Lit(resource.Description)},");
                            if (resource.MimeType is not null)
                                sb.AppendLine($"MimeType = {Lit(resource.MimeType)},");
                        }

                        sb.AppendLine(",");
                    }
                }

                sb.AppendLine(";");
            }
        }

        sb.AppendLine();

        // GetPromptInfos
        sb.AppendLine(
            "public static global::System.Collections.Generic.IReadOnlyList<global::Qyl.Agents.McpPromptInfo> GetPromptInfos()");
        using (sb.BeginBlock())
        {
            if (server.Prompts.IsEmpty)
            {
                sb.AppendLine(
                    "return global::System.Array.Empty<global::Qyl.Agents.McpPromptInfo>();");
            }
            else
            {
                sb.AppendLine("return new global::Qyl.Agents.McpPromptInfo[]");
                using (sb.BeginBlock())
                {
                    foreach (var prompt in server.Prompts)
                    {
                        sb.AppendLine("new global::Qyl.Agents.McpPromptInfo");
                        using (sb.BeginBlock())
                        {
                            sb.AppendLine($"Name = {Lit(prompt.PromptName)},");
                            if (prompt.Description.Length > 0)
                                sb.AppendLine($"Description = {Lit(prompt.Description)},");
                            if (!prompt.Parameters.IsEmpty)
                            {
                                sb.AppendLine(
                                    "Arguments = new global::Qyl.Agents.McpPromptArgument[]");
                                using (sb.BeginBlock())
                                {
                                    foreach (var p in prompt.Parameters)
                                    {
                                        sb.AppendLine("new global::Qyl.Agents.McpPromptArgument");
                                        using (sb.BeginBlock())
                                        {
                                            sb.AppendLine($"Name = {Lit(p.CamelCaseName)},");
                                            if (p.Description is not null)
                                                sb.AppendLine($"Description = {Lit(p.Description)},");
                                            sb.AppendLine($"Required = {(p.IsRequired ? "true" : "false")},");
                                        }

                                        sb.AppendLine(",");
                                    }
                                }

                                sb.AppendLine(",");
                            }
                        }

                        sb.AppendLine(",");
                    }
                }

                sb.AppendLine(";");
            }
        }
    }

    private static string HintBool(ToolHintValue hint)
    {
        return hint == ToolHintValue.True ? "true" : "false";
    }

    private static string Lit(string? value)
    {
        return EmitHelpers.Lit(value);
    }
}
