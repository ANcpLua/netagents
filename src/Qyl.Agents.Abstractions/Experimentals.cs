namespace Qyl.Agents;

/// <summary>
///     Diagnostic IDs for APIs annotated with <see cref="System.Diagnostics.CodeAnalysis.ExperimentalAttribute" />.
///     <list type="bullet">
///         <item><c>QYLEXP001</c> — experimental MCP specification features (Tasks, Extensions).</item>
///         <item><c>QYLEXP002</c> — experimental SDK-level extensibility hooks.</item>
///     </list>
/// </summary>
public static class Experimentals
{
    /// <summary>Diagnostic ID for experimental MCP Tasks feature.</summary>
    public const string Tasks_DiagnosticId = "QYLEXP001";

    /// <summary>Message for experimental MCP Tasks feature.</summary>
    public const string Tasks_Message =
        "The Tasks feature is experimental per the MCP specification and is subject to change.";

    /// <summary>Diagnostic ID for experimental MCP Extensions feature.</summary>
    public const string Extensions_DiagnosticId = "QYLEXP001";

    /// <summary>Message for experimental MCP Extensions feature.</summary>
    public const string Extensions_Message =
        "The Extensions feature is part of a future MCP specification version and is subject to change.";

    /// <summary>Diagnostic ID for experimental SDK extensibility hooks.</summary>
    public const string Sdk_DiagnosticId = "QYLEXP002";

    /// <summary>Message for experimental SDK extensibility hooks.</summary>
    public const string Sdk_Message =
        "This API is experimental and may be removed or changed in a future release.";
}
