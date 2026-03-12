using Qyl.Agents.Generator.Models;

namespace Qyl.Agents.Generator.Generation;

internal static class OTelEmitter
{
    public static void Emit(IndentedStringBuilder sb, ServerModel server)
    {
        // Version from assembly metadata — ties instrumentation version to package version
        sb.AppendLine("private static readonly string s_instrumentationVersion =");
        sb.AppendLine($"    typeof({server.ClassName}).Assembly.GetName().Version?.ToString() ?? \"0.0.0\";");
        sb.AppendLine();

        sb.AppendLine("private static readonly global::System.Diagnostics.ActivitySource s_activitySource =");
        sb.AppendLine("    new global::System.Diagnostics.ActivitySource(\"Qyl.Agents\", s_instrumentationVersion);");
        sb.AppendLine();

        sb.AppendLine("private static readonly global::System.Diagnostics.Metrics.Meter s_meter =");
        sb.AppendLine("    new global::System.Diagnostics.Metrics.Meter(\"Qyl.Agents\", s_instrumentationVersion);");
        sb.AppendLine();

        sb.AppendLine(
            "private static readonly global::System.Diagnostics.Metrics.Histogram<double> s_requestDuration =");
        sb.AppendLine(
            "    s_meter.CreateHistogram<double>(\"gen_ai.client.operation.duration\", \"s\", \"Duration of tool execution\");");
    }
}