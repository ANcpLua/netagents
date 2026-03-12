namespace Qyl.Agents.Generator.Models;

internal readonly record struct
    ServerModel(
        string Namespace,
        string ClassName,
        string ServerName,
        string Description,
        string? Version,
        EquatableArray<TypeDeclarationModel> DeclarationChain,
        EquatableArray<ToolModel> Tools);