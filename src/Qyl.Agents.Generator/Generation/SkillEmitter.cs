namespace Qyl.Agents.Generator.Generation;

using System.Text;
using Models;

internal static class SkillEmitter
{
    public static void Emit(IndentedStringBuilder sb, ServerModel server)
    {
        var content = BuildSkillMdContent(server);
        var escaped = content.Replace("\"", "\"\"");

        sb.AppendLine("private const string s_skillMd = @\"" + escaped + "\";");
        sb.AppendLine();
        sb.AppendLine("/// <summary>Returns the SKILL.md content for dotagents distribution.</summary>");
        sb.AppendLine("public static string SkillMd => s_skillMd;");
    }

    private static string BuildSkillMdContent(ServerModel server)
    {
        var md = new StringBuilder();

        // YAML frontmatter
        md.AppendLine("---");
        md.Append("name: ").AppendLine(server.ServerName);
        EmitYamlValue(md, "description", server.Description);
        md.AppendLine("---");
        md.AppendLine();

        // Header
        md.Append("# ").AppendLine(server.ServerName);
        md.AppendLine();
        md.AppendLine(server.Description);
        md.AppendLine();

        // Tools section
        md.AppendLine("## Tools");
        md.AppendLine();

        foreach (var tool in server.Tools)
        {
            md.Append("### ").AppendLine(tool.ToolName);
            md.AppendLine();
            md.AppendLine(tool.Description);

            AppendAnnotationHints(md, tool);
            md.AppendLine();

            if (!tool.Parameters.IsEmpty)
            {
                md.AppendLine("**Parameters:**");
                md.AppendLine();
                foreach (var p in tool.Parameters)
                {
                    md.Append("- `").Append(p.CamelCaseName).Append("` (").Append(p.JsonSchemaType);
                    if (p.IsRequired)
                        md.Append(", required");
                    md.Append(')');
                    if (p.Description is not null)
                        md.Append(": ").Append(p.Description);
                    md.AppendLine();
                }

                md.AppendLine();
            }
        }

        // Resources section
        if (!server.Resources.IsEmpty)
        {
            md.AppendLine("## Resources");
            md.AppendLine();

            foreach (var resource in server.Resources)
            {
                md.Append("- `").Append(resource.Uri).Append('`');
                if (resource.MimeType is not null)
                    md.Append(" (").Append(resource.MimeType).Append(')');
                if (resource.Description is not null)
                    md.Append(": ").Append(resource.Description);
                md.AppendLine();
            }

            md.AppendLine();
        }

        // Prompts section
        if (!server.Prompts.IsEmpty)
        {
            md.AppendLine("## Prompts");
            md.AppendLine();

            foreach (var prompt in server.Prompts)
            {
                md.Append("### ").AppendLine(prompt.PromptName);
                md.AppendLine();
                if (prompt.Description.Length > 0)
                    md.AppendLine(prompt.Description);

                if (!prompt.Parameters.IsEmpty)
                {
                    md.AppendLine();
                    md.AppendLine("**Arguments:**");
                    md.AppendLine();
                    foreach (var p in prompt.Parameters)
                    {
                        md.Append("- `").Append(p.CamelCaseName).Append('`');
                        if (p.IsRequired)
                            md.Append(" (required)");
                        if (p.Description is not null)
                            md.Append(": ").Append(p.Description);
                        md.AppendLine();
                    }
                }

                md.AppendLine();
            }
        }

        return md.ToString().TrimEnd();
    }

    private static void AppendAnnotationHints(StringBuilder md, ToolModel tool)
    {
        var hints = new List<string>();
        if (tool.ReadOnly != ToolHintValue.Unset)
            hints.Add(tool.ReadOnly == ToolHintValue.True ? "read-only" : "not read-only");
        if (tool.Destructive != ToolHintValue.Unset)
            hints.Add(tool.Destructive == ToolHintValue.True ? "destructive" : "not destructive");
        if (tool.Idempotent != ToolHintValue.Unset)
            hints.Add(tool.Idempotent == ToolHintValue.True ? "idempotent" : "not idempotent");
        if (tool.OpenWorld != ToolHintValue.Unset)
            hints.Add(tool.OpenWorld == ToolHintValue.True ? "open-world" : "not open-world");

        if (hints.Count > 0)
            md.AppendLine().Append("*Annotations: ").Append(string.Join(", ", hints)).AppendLine("*");
    }

    private static void EmitYamlValue(StringBuilder sb, string key, string value)
    {
        if (value.Contains('\n'))
        {
            sb.Append(key).AppendLine(": |");
            foreach (var line in value.Split('\n'))
                sb.Append("  ").AppendLine(line.TrimEnd('\r'));
        }
        else if (value.Contains(':') || value.Contains('#') || value.Contains('"') ||
                 (value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[value.Length - 1]))))
        {
            sb.Append(key).Append(": \"")
                .Append(value.Replace("\\", "\\\\").Replace("\"", "\\\""))
                .AppendLine("\"");
        }
        else
        {
            sb.Append(key).Append(": ").AppendLine(value);
        }
    }
}
