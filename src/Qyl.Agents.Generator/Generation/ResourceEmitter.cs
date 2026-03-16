namespace Qyl.Agents.Generator.Generation;

using Models;

internal static class ResourceEmitter
{
    public static void Emit(IndentedStringBuilder sb, ServerModel server)
    {
        if (server.Resources.IsEmpty)
        {
            // Emit stub that throws for unknown URIs
            sb.AppendLine(
                "public async global::System.Threading.Tasks.Task<global::Qyl.Agents.ResourceReadResult> DispatchResourceReadAsync(");
            sb.AppendLine("    string uri,");
            sb.AppendLine("    global::System.Threading.CancellationToken cancellationToken = default)");
            using (sb.BeginBlock())
            {
                sb.AppendLine("await global::System.Threading.Tasks.Task.CompletedTask;");
                sb.AppendLine(
                    "throw new global::System.ArgumentException($\"Unknown resource: {uri}\", nameof(uri));");
            }

            sb.AppendLine();
            return;
        }

        // Public dispatch entry point
        sb.AppendLine(
            "public async global::System.Threading.Tasks.Task<global::Qyl.Agents.ResourceReadResult> DispatchResourceReadAsync(");
        sb.AppendLine("    string uri,");
        sb.AppendLine("    global::System.Threading.CancellationToken cancellationToken = default)");
        using (sb.BeginBlock())
        {
            sb.AppendLine("return uri switch");
            using (sb.BeginBlock())
            {
                foreach (var resource in server.Resources)
                    sb.AppendLine(
                        $"{Lit(resource.Uri)} => await {PerResourceMethod(resource)}(cancellationToken),");

                sb.AppendLine(
                    "_ => throw new global::System.ArgumentException($\"Unknown resource: {uri}\", nameof(uri))");
            }

            sb.AppendLine(";");
        }

        sb.AppendLine();

        // Per-resource private methods
        foreach (var resource in server.Resources)
            EmitPerResourceMethod(sb, resource);
    }

    private static void EmitPerResourceMethod(IndentedStringBuilder sb, ResourceModel resource)
    {
        sb.AppendLine(
            $"private async global::System.Threading.Tasks.Task<global::Qyl.Agents.ResourceReadResult> {PerResourceMethod(resource)}(");
        sb.AppendLine("    global::System.Threading.CancellationToken cancellationToken)");
        using (sb.BeginBlock())
        {
            var ctArg = resource.HasCancellationToken ? "cancellationToken" : "";
            var needsAwait = resource.ReturnKind is ReturnKind.TaskOfT or ReturnKind.ValueTaskOfT;

            if (!needsAwait)
                sb.AppendLine("await global::System.Threading.Tasks.Task.CompletedTask;");

            sb.AppendLine(
                $"var raw = {(needsAwait ? "await " : "")}{resource.MethodName}({ctArg});");

            if (resource.IsBinary)
            {
                sb.AppendLine(
                    "return new global::Qyl.Agents.ResourceReadResult(global::System.Convert.ToBase64String(raw), true);");
            }
            else
            {
                sb.AppendLine(
                    "return new global::Qyl.Agents.ResourceReadResult(raw, false);");
            }
        }

        sb.AppendLine();
    }

    private static string PerResourceMethod(ResourceModel resource)
    {
        return $"ReadResource_{resource.MethodName}Async";
    }

    private static string Lit(string value)
    {
        return EmitHelpers.Lit(value);
    }
}
