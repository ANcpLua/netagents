namespace Qyl.Agents.Generator.Generation;

using Models;

internal static class PromptEmitter
{
    private const string PromptResultFqn = "global::Qyl.Agents.PromptResult";

    public static void Emit(IndentedStringBuilder sb, ServerModel server)
    {
        if (server.Prompts.IsEmpty)
        {
            // Emit stub that throws for unknown prompts
            sb.AppendLine(
                $"public async global::System.Threading.Tasks.Task<{PromptResultFqn}> DispatchPromptAsync(");
            sb.AppendLine("    string name,");
            sb.AppendLine("    global::System.Text.Json.JsonElement arguments,");
            sb.AppendLine("    global::System.Threading.CancellationToken cancellationToken = default)");
            using (sb.BeginBlock())
            {
                sb.AppendLine("await global::System.Threading.Tasks.Task.CompletedTask;");
                sb.AppendLine(
                    "throw new global::System.ArgumentException($\"Unknown prompt: {name}\", nameof(name));");
            }

            sb.AppendLine();
            return;
        }

        // Public dispatch entry point
        sb.AppendLine(
            $"public async global::System.Threading.Tasks.Task<{PromptResultFqn}> DispatchPromptAsync(");
        sb.AppendLine("    string name,");
        sb.AppendLine("    global::System.Text.Json.JsonElement arguments,");
        sb.AppendLine("    global::System.Threading.CancellationToken cancellationToken = default)");
        using (sb.BeginBlock())
        {
            sb.AppendLine("return name switch");
            using (sb.BeginBlock())
            {
                foreach (var prompt in server.Prompts)
                    sb.AppendLine(
                        $"{Lit(prompt.PromptName)} => await {PerPromptMethod(prompt)}(arguments, cancellationToken),");

                sb.AppendLine(
                    "_ => throw new global::System.ArgumentException($\"Unknown prompt: {name}\", nameof(name))");
            }

            sb.AppendLine(";");
        }

        sb.AppendLine();

        // Per-prompt private methods
        foreach (var prompt in server.Prompts)
            EmitPerPromptMethod(sb, prompt);
    }

    private static void EmitPerPromptMethod(IndentedStringBuilder sb, PromptModel prompt)
    {
        sb.AppendLine(
            $"private async global::System.Threading.Tasks.Task<{PromptResultFqn}> {PerPromptMethod(prompt)}(");
        sb.AppendLine("    global::System.Text.Json.JsonElement args,");
        sb.AppendLine("    global::System.Threading.CancellationToken cancellationToken)");
        using (sb.BeginBlock())
        {
            var needsAwait = prompt.ReturnKind is ReturnKind.TaskOfT or ReturnKind.ValueTaskOfT;

            if (!needsAwait)
                sb.AppendLine("await global::System.Threading.Tasks.Task.CompletedTask;");

            // Deserialize parameters
            foreach (var p in prompt.Parameters)
                EmitParameterDeserialization(sb, p);

            // Build call args
            var callArgs = BuildCallArgs(prompt);

            if (prompt.IsStructured)
            {
                // PromptResult return — pass through directly
                sb.AppendLine(
                    $"var result = {(needsAwait ? "await " : "")}{prompt.MethodName}({callArgs});");
                sb.AppendLine("return result;");
            }
            else
            {
                // String return — wrap in a single user message
                sb.AppendLine(
                    $"var result = {(needsAwait ? "await " : "")}{prompt.MethodName}({callArgs});");
                sb.AppendLine($"return new {PromptResultFqn}(new global::Qyl.Agents.PromptMessage[]");
                using (sb.BeginBlock())
                {
                    sb.AppendLine(
                        "new global::Qyl.Agents.PromptMessage(global::Qyl.Agents.PromptRole.User, result),");
                }

                sb.AppendLine(");");
            }
        }

        sb.AppendLine();
    }

    private static void EmitParameterDeserialization(IndentedStringBuilder sb, ToolParameterModel p)
    {
        // Simplified deserialization for prompt parameters — strings only in typical usage
        if (p.IsRequired)
        {
            sb.AppendLine(
                $"var {p.CamelCaseName} = args.GetProperty({Lit(p.CamelCaseName)}).GetString() ?? throw new global::System.Text.Json.JsonException(\"Required parameter '{p.CamelCaseName}' is null\");");
        }
        else
        {
            var defaultValue = p.DefaultValueLiteral ?? $"default({p.TypeFullyQualified})";
            sb.AppendLine(
                $"var {p.CamelCaseName} = args.TryGetProperty({Lit(p.CamelCaseName)}, out var _{p.CamelCaseName}El) ? _{p.CamelCaseName}El.GetString() : {defaultValue};");
        }
    }

    private static string BuildCallArgs(PromptModel prompt)
    {
        var parts = prompt.Parameters.Select(static p => p.CamelCaseName).ToList();
        if (prompt.HasCancellationToken)
            parts.Add("cancellationToken");
        return string.Join(", ", parts);
    }

    private static string PerPromptMethod(PromptModel prompt)
    {
        return $"ExecutePrompt_{prompt.MethodName}Async";
    }

    private static string Lit(string value)
    {
        return EmitHelpers.Lit(value);
    }
}
