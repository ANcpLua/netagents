namespace Qyl.Agents.Generator.Generation;

using System.Text;
using Models;

internal static class SchemaEmitter
{
    public static void Emit(IndentedStringBuilder sb, ServerModel server)
    {
        foreach (var tool in server.Tools)
        {
            var fieldName = $"s_schema_{tool.MethodName}";
            var json = BuildSchema(tool);
            var escaped = json.Replace("\"", "\"\"");

            sb.AppendLine(
                $"private static readonly byte[] {fieldName} = global::System.Text.Encoding.UTF8.GetBytes(@\"{escaped}\");");
            sb.AppendLine();
        }
    }

    internal static string BuildSchema(ToolModel tool)
    {
        var sb = new StringBuilder();
        sb.Append("{\"type\":\"object\"");

        if (!tool.Parameters.IsEmpty)
        {
            sb.Append(",\"properties\":{");
            var first = true;
            foreach (var p in tool.Parameters)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append($"\"{p.CamelCaseName}\":{{");
                sb.Append($"\"type\":\"{p.JsonSchemaType}\"");
                if (p.JsonSchemaFormat is not null)
                    sb.Append($",\"format\":\"{p.JsonSchemaFormat}\"");
                if (p.Description is not null)
                    sb.Append($",\"description\":\"{EscapeJson(p.Description)}\"");
                if (!p.EnumValues.IsEmpty)
                {
                    sb.Append(",\"enum\":[");
                    var firstEnum = true;
                    foreach (var v in p.EnumValues)
                    {
                        if (!firstEnum) sb.Append(',');
                        firstEnum = false;
                        sb.Append($"\"{v}\"");
                    }

                    sb.Append(']');
                }

                sb.Append('}');
            }

            sb.Append('}');

            var required = tool.Parameters
                .Where(static p => p.IsRequired)
                .Select(static p => $"\"{p.CamelCaseName}\"")
                .ToArray();
            if (required.Length > 0)
                sb.Append($",\"required\":[{string.Join(",", required)}]");
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static string EscapeJson(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < ' ')
                        sb.Append($"\\u{(int)c:X4}");
                    else
                        sb.Append(c);
                    break;
            }

        return sb.ToString();
    }
}
