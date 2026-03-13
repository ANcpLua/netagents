namespace Qyl.Agents.Generator.Generation;

using Models;

internal static class JsonContextEmitter
{
    public static void Emit(IndentedStringBuilder sb, ServerModel server)
    {
        var types = CollectSerializableTypes(server);
        if (types.Count == 0) return;

        sb.AppendLine("[global::System.Text.Json.Serialization.JsonSourceGenerationOptions(");
        sb.AppendLine("    global::System.Text.Json.JsonSerializerDefaults.Web)]");

        foreach (var type in types)
            sb.AppendLine($"[global::System.Text.Json.Serialization.JsonSerializable(typeof({type}))]");

        sb.AppendLine($"private partial class {server.ClassName}JsonContext");
        sb.AppendLine("    : global::System.Text.Json.Serialization.JsonSerializerContext { }");
    }

    private static List<string> CollectSerializableTypes(ServerModel server)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();

        foreach (var tool in server.Tools)
        {
            // Parameter types (deserialization)
            foreach (var p in tool.Parameters)
            {
                if (seen.Add(p.TypeFullyQualified))
                    result.Add(p.TypeFullyQualified);

                // Nullable<T> wrapper for nullable value type parameters
                if (p.IsNullable && p.IsValueType)
                {
                    var nullableType = $"global::System.Nullable<{p.TypeFullyQualified}>";
                    if (seen.Add(nullableType))
                        result.Add(nullableType);
                }
            }

            // Return types (serialization) — only for kinds that produce a value
            if (tool.ReturnKind is ReturnKind.Void or ReturnKind.Task or ReturnKind.ValueTask)
                continue;
            if (seen.Add(tool.ResultTypeFullyQualified))
                result.Add(tool.ResultTypeFullyQualified);
        }

        return result;
    }
}
