namespace Qyl.Agents.Generator.Models;

internal enum ReturnKind : byte
{
    Void,
    Sync,
    Task,
    ValueTask,
    TaskOfT,
    ValueTaskOfT
}

internal readonly record struct ToolModel(
    string MethodName,
    string ToolName,
    string Description,
    string ResultTypeFullyQualified,
    ReturnKind ReturnKind,
    bool HasCancellationToken,
    EquatableArray<ToolParameterModel> Parameters);
