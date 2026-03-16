namespace Qyl.Agents.Generator.Models;

internal readonly record struct ResourceModel(
    string MethodName,
    string Uri,
    string? Name,
    string? MimeType,
    string? Description,
    string ResultTypeFullyQualified,
    ReturnKind ReturnKind,
    bool HasCancellationToken,
    bool IsBinary);
