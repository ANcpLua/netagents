namespace Qyl.Agents.Generator;

internal static class DiagnosticDescriptors
{
    private const string Category = "Usage";

    public static readonly DiagnosticDescriptor ClassMustBePartial = new(
        "QA0001",
        "McpServer class must be partial",
        "Class '{0}' is decorated with [McpServer] but is not declared partial",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor ClassMustNotBeStatic = new(
        "QA0002",
        "McpServer class must not be static",
        "Class '{0}' is decorated with [McpServer] but is declared static",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor ClassMustNotBeGeneric = new(
        "QA0003",
        "McpServer class must not be generic",
        "Class '{0}' is decorated with [McpServer] but is generic",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor ToolMethodMustBeInsideMcpServer = new(
        "QA0004",
        "Tool method must be inside McpServer class",
        "Method '{0}' is decorated with [Tool] but its containing class is not decorated with [McpServer]",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor ToolMethodMustNotBeStatic = new(
        "QA0005",
        "Tool method must not be static",
        "Method '{0}' decorated with [Tool] must not be static",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor ToolMethodMustNotBeGeneric = new(
        "QA0006",
        "Tool method must not be generic",
        "Method '{0}' decorated with [Tool] must not be generic",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor UnsupportedReturnType = new(
        "QA0007",
        "Tool method has unsupported return type",
        "Method '{0}' has unsupported return type '{1}' — supported types are void, T, Task, Task<T>, ValueTask, and ValueTask<T>",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor UnsupportedParameterType = new(
        "QA0008",
        "Tool parameter has unsupported type",
        "Parameter '{0}' of method '{1}' has unsupported type '{2}'",
        Category,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor ParameterMissingDescription = new(
        "QA0009",
        "Tool parameter has no Description",
        "Parameter '{0}' of tool method '{1}' has no [Description] attribute",
        Category,
        DiagnosticSeverity.Warning,
        true);

    public static readonly DiagnosticDescriptor NoToolsFound = new(
        "QA0010",
        "McpServer class has no Tool methods",
        "Class '{0}' is decorated with [McpServer] but has no methods decorated with [Tool]",
        Category,
        DiagnosticSeverity.Warning,
        true);

    public static readonly DiagnosticDescriptor DuplicateToolName = new(
        "QA0011",
        "Duplicate tool name",
        "Tool name '{0}' is used by multiple methods in class '{1}'",
        Category,
        DiagnosticSeverity.Error,
        true);
}