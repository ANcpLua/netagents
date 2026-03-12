using Qyl.Agents.Generator.Models;

namespace Qyl.Agents.Generator.Generation;

internal static class DispatchEmitter
{
    public static void Emit(IndentedStringBuilder sb, ServerModel server)
    {
        // Only emit s_jsonOptions when complex types (array/object) require JsonSerializer fallback
        if (HasComplexTypes(server))
        {
            sb.AppendLine(
                "private static readonly global::System.Text.Json.JsonSerializerOptions s_jsonOptions = new()");
            using (sb.BeginBlock())
            {
                sb.AppendLine("PropertyNamingPolicy = global::System.Text.Json.JsonNamingPolicy.CamelCase,");
                sb.AppendLine(
                    "DefaultIgnoreCondition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,");
            }

            sb.AppendLine(";");
            sb.AppendLine();
        }

        // Public dispatch entry point
        sb.AppendLine("public async global::System.Threading.Tasks.Task<string> DispatchToolCallAsync(");
        sb.AppendLine("    string toolName,");
        sb.AppendLine("    global::System.Text.Json.JsonElement arguments,");
        sb.AppendLine("    global::System.Threading.CancellationToken cancellationToken = default)");
        using (sb.BeginBlock())
        {
            sb.AppendLine("return toolName switch");
            using (sb.BeginBlock())
            {
                foreach (var tool in server.Tools)
                    sb.AppendLine(
                        $"{Lit(tool.ToolName)} => await {PerToolMethod(tool)}(arguments, cancellationToken),");

                sb.AppendLine(
                    "_ => throw new global::System.ArgumentException($\"Unknown tool: {toolName}\", nameof(toolName))");
            }

            sb.AppendLine(";");
        }

        sb.AppendLine();

        // Per-tool private methods
        foreach (var tool in server.Tools)
            EmitPerToolMethod(sb, tool, server);
    }

    private static void EmitPerToolMethod(IndentedStringBuilder sb, ToolModel tool, ServerModel server)
    {
        sb.AppendLine($"private async global::System.Threading.Tasks.Task<string> {PerToolMethod(tool)}(");
        sb.AppendLine("    global::System.Text.Json.JsonElement args,");
        sb.AppendLine("    global::System.Threading.CancellationToken cancellationToken)");
        using (sb.BeginBlock())
        {
            // OTel span
            sb.AppendLine("using var activity = s_activitySource.StartActivity(");
            sb.AppendLine($"    $\"execute_tool {tool.ToolName}\",");
            sb.AppendLine("    global::System.Diagnostics.ActivityKind.Internal);");
            sb.AppendLine("if (activity is not null)");
            using (sb.BeginBlock())
            {
                sb.AppendLine("activity.SetTag(\"gen_ai.operation.name\", \"execute_tool\");");
                sb.AppendLine($"activity.SetTag(\"gen_ai.tool.name\", {Lit(tool.ToolName)});");
                sb.AppendLine("activity.SetTag(\"gen_ai.tool.type\", \"function\");");
                sb.AppendLine("activity.SetTag(\"gen_ai.system\", \"mcp\");");
                sb.AppendLine($"activity.SetTag(\"server.name\", {Lit(server.ServerName)});");
            }

            sb.AppendLine();

            // Metrics
            sb.AppendLine("var sw = global::System.Diagnostics.Stopwatch.StartNew();");
            sb.AppendLine("try");
            using (sb.BeginBlock())
            {
                // Deserialize parameters — AOT-safe direct accessors for primitives
                foreach (var p in tool.Parameters)
                    EmitParameterDeserialization(sb, p);

                // Call user method — switch on ReturnKind
                var callArgs = BuildCallArgs(tool);
                var needsAwait = tool.ReturnKind is ReturnKind.Task or ReturnKind.ValueTask
                    or ReturnKind.TaskOfT or ReturnKind.ValueTaskOfT;
                var hasResult = tool.ReturnKind is ReturnKind.Sync or ReturnKind.TaskOfT or ReturnKind.ValueTaskOfT;

                if (hasResult)
                {
                    sb.AppendLine($"var result = {(needsAwait ? "await " : "")}{tool.MethodName}({callArgs});");
                    sb.AppendLine("activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Ok);");
                    EmitReturnSerialization(sb, tool);
                }
                else
                {
                    sb.AppendLine($"{(needsAwait ? "await " : "")}{tool.MethodName}({callArgs});");
                    sb.AppendLine("activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Ok);");
                    sb.AppendLine("return \"null\";");
                }
            }

            sb.AppendLine("catch (global::System.Exception ex)");
            using (sb.BeginBlock())
            {
                sb.AppendLine("activity?.SetTag(\"error.type\", ex.GetType().FullName);");
                sb.AppendLine("activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Error, ex.Message);");
                sb.AppendLine("throw;");
            }

            sb.AppendLine("finally");
            using (sb.BeginBlock())
            {
                sb.AppendLine("s_requestDuration.Record(sw.Elapsed.TotalSeconds);");
            }
        }

        sb.AppendLine();
    }

    // ── Parameter deserialization ─────────────────────────────────────────────

    private static void EmitParameterDeserialization(IndentedStringBuilder sb, ToolParameterModel p)
    {
        var accessor = GetCoreAccessor(p);

        if (accessor is not null)
            EmitDirectDeserialization(sb, p, accessor);
        else
            EmitFallbackDeserialization(sb, p);
    }

    private static void EmitDirectDeserialization(IndentedStringBuilder sb, ToolParameterModel p, string accessor)
    {
        if (p.IsRequired)
        {
            if (p.IsNullable && p.IsValueType)
            {
                // Required nullable value type (e.g., int?) — null check on element
                sb.AppendLine($"var _{p.CamelCaseName}El = args.GetProperty({Lit(p.CamelCaseName)});");
                var expr = string.Format(accessor, $"_{p.CamelCaseName}El");
                sb.AppendLine(
                    $"var {p.CamelCaseName} = _{p.CamelCaseName}El.ValueKind == global::System.Text.Json.JsonValueKind.Null ? default({p.TypeFullyQualified}) : ({p.TypeFullyQualified}){expr};");
            }
            else if (!p.IsNullable && !p.IsValueType)
            {
                // Required non-nullable reference type (e.g., string) — null guard
                var expr = string.Format(accessor, $"args.GetProperty({Lit(p.CamelCaseName)})");
                sb.AppendLine(
                    $"var {p.CamelCaseName} = {expr} ?? throw new global::System.Text.Json.JsonException(\"Required parameter '{p.CamelCaseName}' is null\");");
            }
            else
            {
                // Required non-nullable value type or nullable reference type
                var expr = string.Format(accessor, $"args.GetProperty({Lit(p.CamelCaseName)})");
                sb.AppendLine($"var {p.CamelCaseName} = {expr};");
            }
        }
        else
        {
            var defaultValue = p.DefaultValueLiteral ?? $"default({p.TypeFullyQualified})";

            if (p.IsNullable && p.IsValueType)
            {
                // Optional nullable value type — null check when present
                var expr = string.Format(accessor, $"_{p.CamelCaseName}El");
                sb.AppendLine(
                    $"var {p.CamelCaseName} = args.TryGetProperty({Lit(p.CamelCaseName)}, out var _{p.CamelCaseName}El) && _{p.CamelCaseName}El.ValueKind != global::System.Text.Json.JsonValueKind.Null ? ({p.TypeFullyQualified}){expr} : {defaultValue};");
            }
            else
            {
                var expr = string.Format(accessor, $"_{p.CamelCaseName}El");
                sb.AppendLine(
                    $"var {p.CamelCaseName} = args.TryGetProperty({Lit(p.CamelCaseName)}, out var _{p.CamelCaseName}El) ? {expr} : {defaultValue};");
            }
        }
    }

    private static void EmitFallbackDeserialization(IndentedStringBuilder sb, ToolParameterModel p)
    {
        // Fallback to JsonSerializer for complex types (array, object) — still has AOT warning
        if (p.IsRequired && !p.IsNullable && !p.IsValueType)
            sb.AppendLine(
                $"var {p.CamelCaseName} = global::System.Text.Json.JsonSerializer.Deserialize<{p.TypeFullyQualified}>(args.GetProperty({Lit(p.CamelCaseName)}), s_jsonOptions) ?? throw new global::System.Text.Json.JsonException(\"Required parameter '{p.CamelCaseName}' deserialized to null\");");
        else if (p.IsRequired)
            sb.AppendLine(
                $"var {p.CamelCaseName} = global::System.Text.Json.JsonSerializer.Deserialize<{p.TypeFullyQualified}>(args.GetProperty({Lit(p.CamelCaseName)}), s_jsonOptions);");
        else if (p.DefaultValueLiteral is not null)
            sb.AppendLine(
                $"var {p.CamelCaseName} = args.TryGetProperty({Lit(p.CamelCaseName)}, out var _{p.CamelCaseName}El) ? global::System.Text.Json.JsonSerializer.Deserialize<{p.TypeFullyQualified}>(_{p.CamelCaseName}El, s_jsonOptions) : {p.DefaultValueLiteral};");
        else
            sb.AppendLine(
                $"var {p.CamelCaseName} = args.TryGetProperty({Lit(p.CamelCaseName)}, out var _{p.CamelCaseName}El) ? global::System.Text.Json.JsonSerializer.Deserialize<{p.TypeFullyQualified}>(_{p.CamelCaseName}El, s_jsonOptions) : default({p.TypeFullyQualified});");
    }

    // ── Return serialization ─────────────────────────────────────────────────

    private static void EmitReturnSerialization(IndentedStringBuilder sb, ToolModel tool)
    {
        var rt = tool.ResultTypeFullyQualified;

        // String return — direct, no serialization overhead
        if (rt.IsStringType())
        {
            sb.AppendLine("return result;");
            return;
        }

        // Boolean — manual to produce JSON-compatible "true"/"false"
        if (rt.TypeNamesEqual("bool"))
        {
            sb.AppendLine("return result ? \"true\" : \"false\";");
            return;
        }

        // Integer/number types — culture-invariant ToString
        if (IsNumericType(rt))
        {
            sb.AppendLine("return result.ToString(global::System.Globalization.CultureInfo.InvariantCulture);");
            return;
        }

        // Fallback — JsonSerializer for complex return types (still has AOT warning)
        sb.AppendLine("return global::System.Text.Json.JsonSerializer.Serialize(result, s_jsonOptions);");
    }

    // ── Accessor mapping ─────────────────────────────────────────────────────

    /// <summary>
    ///     Returns a format string for the direct JsonElement accessor where {0} is the element expression.
    ///     Returns null if the type requires JsonSerializer fallback (array, object).
    /// </summary>
    private static string? GetCoreAccessor(ToolParameterModel p)
    {
        // Enum — parse from string
        if (!p.EnumValues.IsEmpty)
        {
            var enumType = p.TypeFullyQualified.UnwrapNullable();
            return $"global::System.Enum.Parse<{enumType}>({{0}}.GetString()!)";
        }

        return p.JsonSchemaType switch
        {
            "string" when p.JsonSchemaFormat is "date-time" && p.TypeFullyQualified.Contains("DateTimeOffset") =>
                "{0}.GetDateTimeOffset()",
            "string" when p.JsonSchemaFormat is "date-time" => "{0}.GetDateTime()",
            "string" when p.JsonSchemaFormat is "uuid" => "{0}.GetGuid()",
            "string" when p.JsonSchemaFormat is "uri" => "new global::System.Uri({0}.GetString()!)",
            "string" => "{0}.GetString()",
            "boolean" => "{0}.GetBoolean()",
            "integer" when p.TypeFullyQualified.Contains("System.Int64") => "{0}.GetInt64()",
            "integer" when p.TypeFullyQualified.Contains("System.UInt64") => "{0}.GetUInt64()",
            "integer" when p.TypeFullyQualified.Contains("System.Int16") => "{0}.GetInt16()",
            "integer" when p.TypeFullyQualified.Contains("System.UInt16") => "{0}.GetUInt16()",
            "integer" when p.TypeFullyQualified.Contains("System.UInt32") => "{0}.GetUInt32()",
            "integer" when p.TypeFullyQualified.Contains("System.SByte") => "{0}.GetSByte()",
            "integer" when p.TypeFullyQualified.Contains("System.Byte") &&
                           !p.TypeFullyQualified.Contains("System.SByte") => "{0}.GetByte()",
            "integer" => "{0}.GetInt32()",
            "number" when p.TypeFullyQualified.Contains("System.Single") => "{0}.GetSingle()",
            "number" when p.TypeFullyQualified.Contains("System.Decimal") => "{0}.GetDecimal()",
            "number" => "{0}.GetDouble()",
            _ => null // array, object → JsonSerializer fallback
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool HasComplexTypes(ServerModel server)
    {
        foreach (var tool in server.Tools)
        {
            foreach (var p in tool.Parameters)
                if (GetCoreAccessor(p) is null)
                    return true;

            if (tool.ReturnKind is ReturnKind.Sync or ReturnKind.TaskOfT or ReturnKind.ValueTaskOfT)
            {
                var rt = tool.ResultTypeFullyQualified;
                if (!IsDirectlySerializableReturn(rt))
                    return true;
            }
        }

        return false;
    }

    private static bool IsDirectlySerializableReturn(string rt) =>
        rt.IsStringType() || rt.TypeNamesEqual("bool") || IsNumericType(rt);

    private static bool IsNumericType(string typeFullyQualified)
    {
        return typeFullyQualified.Contains("System.Int16") ||
               typeFullyQualified.Contains("System.Int32") ||
               typeFullyQualified.Contains("System.Int64") ||
               typeFullyQualified.Contains("System.UInt16") ||
               typeFullyQualified.Contains("System.UInt32") ||
               typeFullyQualified.Contains("System.UInt64") ||
               typeFullyQualified.Contains("System.Byte") ||
               typeFullyQualified.Contains("System.SByte") ||
               typeFullyQualified.Contains("System.Double") ||
               typeFullyQualified.Contains("System.Single") ||
               typeFullyQualified.Contains("System.Decimal");
    }

    private static string BuildCallArgs(ToolModel tool)
    {
        var parts = tool.Parameters.Select(static p => p.CamelCaseName).ToList();
        if (tool.HasCancellationToken)
            parts.Add("cancellationToken");
        return string.Join(", ", parts);
    }

    private static string PerToolMethod(ToolModel tool)
    {
        return $"ExecuteTool_{tool.MethodName}Async";
    }

    private static string Lit(string value) => EmitHelpers.Lit(value);
}