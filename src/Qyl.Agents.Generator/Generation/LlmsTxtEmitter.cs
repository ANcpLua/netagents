namespace Qyl.Agents.Generator.Generation;

using System.Text;
using Models;

// NOTE: The /mcp link target is the JSON-RPC endpoint. An LLM crawler hitting it via GET
// won't get useful output — this is a spec limitation, not an implementation bug.
internal static class LlmsTxtEmitter
{
    public static void Emit(IndentedStringBuilder sb, ServerModel server)
    {
        var content = BuildLlmsTxtContent(server);
        var escaped = content.Replace("\"", "\"\"");

        sb.AppendLine("private const string s_llmsTxt = @\"" + escaped + "\";");
        sb.AppendLine();
        sb.AppendLine("/// <summary>Returns the llms.txt content for LLM indexing.</summary>");
        sb.AppendLine("public static string LlmsTxt => s_llmsTxt;");
    }

    private static string BuildLlmsTxtContent(ServerModel server)
    {
        var txt = new StringBuilder();

        txt.Append("# ").AppendLine(server.ServerName);
        txt.AppendLine();
        txt.Append("> ").AppendLine(server.Description);

        if (!server.Tools.IsEmpty)
        {
            txt.AppendLine();
            txt.AppendLine("## Tools");
            txt.AppendLine();

            foreach (var tool in server.Tools)
            {
                txt.Append("- [").Append(tool.ToolName).Append("](/mcp): ").Append(tool.Description);
                var hints = BuildHintSuffix(tool);
                if (hints.Length > 0)
                    txt.Append(" (").Append(hints).Append(')');
                txt.AppendLine();
            }
        }

        if (!server.Resources.IsEmpty)
        {
            txt.AppendLine();
            txt.AppendLine("## Resources");
            txt.AppendLine();

            foreach (var resource in server.Resources)
            {
                txt.Append("- [").Append(resource.Uri).Append("](/mcp)");
                if (resource.Description is not null)
                    txt.Append(": ").Append(resource.Description);
                if (resource.MimeType is not null)
                    txt.Append(" (").Append(resource.MimeType).Append(')');
                txt.AppendLine();
            }
        }

        if (!server.Prompts.IsEmpty)
        {
            txt.AppendLine();
            txt.AppendLine("## Prompts");
            txt.AppendLine();

            foreach (var prompt in server.Prompts)
            {
                txt.Append("- [").Append(prompt.PromptName).Append("](/mcp)");
                if (prompt.Description.Length > 0)
                    txt.Append(": ").Append(prompt.Description);
                if (!prompt.Parameters.IsEmpty)
                {
                    txt.Append(" (arguments: ");
                    var first = true;
                    foreach (var p in prompt.Parameters)
                    {
                        if (!first) txt.Append(", ");
                        first = false;
                        txt.Append(p.CamelCaseName);
                    }
                    txt.Append(')');
                }
                txt.AppendLine();
            }
        }

        return txt.ToString().TrimEnd();
    }

    private static string BuildHintSuffix(ToolModel tool)
    {
        var parts = new List<string>();
        if (tool.ReadOnly != ToolHintValue.Unset)
            parts.Add(tool.ReadOnly == ToolHintValue.True ? "read-only" : "not read-only");
        if (tool.Destructive != ToolHintValue.Unset)
            parts.Add(tool.Destructive == ToolHintValue.True ? "destructive" : "not destructive");
        if (tool.Idempotent != ToolHintValue.Unset)
            parts.Add(tool.Idempotent == ToolHintValue.True ? "idempotent" : "not idempotent");
        if (tool.OpenWorld != ToolHintValue.Unset)
            parts.Add(tool.OpenWorld == ToolHintValue.True ? "open-world" : "not open-world");
        return string.Join(", ", parts);
    }
}
