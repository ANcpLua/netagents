namespace Qyl.Agents.Generator.Models;

internal readonly record struct PromptModel(
    string MethodName,
    string PromptName,
    string Description,
    string ResultTypeFullyQualified,
    ReturnKind ReturnKind,
    bool HasCancellationToken,
    bool IsStructured,
    EquatableArray<ToolParameterModel> Parameters);
