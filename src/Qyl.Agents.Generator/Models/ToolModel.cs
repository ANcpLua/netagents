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
    EquatableArray<ToolParameterModel> Parameters,
    ToolHintValue ReadOnly,
    ToolHintValue Destructive,
    ToolHintValue Idempotent,
    ToolHintValue OpenWorld,
    ToolTaskSupportValue TaskSupport)
{
    public byte ReadOnlyHint => (byte)ReadOnly;
    public byte IdempotentHint => (byte)Idempotent;
    public byte DestructiveHint => (byte)Destructive;
    public byte OpenWorldHint => (byte)OpenWorld;
}
