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

        return md.ToString().TrimEnd();
    }

    private static void EmitYamlValue(StringBuilder sb, string key, string value)
    {
        if (value.Contains('\n'))
        {
            // Multi-line: use YAML literal block scalar
            sb.Append(key).AppendLine(": |");
            foreach (var line in value.Split('\n'))
                sb.Append("  ").AppendLine(line.TrimEnd('\r'));
        }
        else if (value.Contains(':') || value.Contains('#') || value.Contains('"') ||
                 (value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[value.Length - 1]))))
        {
            // Single-line with special chars: double-quoted scalar
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
