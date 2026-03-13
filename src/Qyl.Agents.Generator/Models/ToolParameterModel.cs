namespace Qyl.Agents.Generator.Models;

internal readonly record struct ToolParameterModel(
    string Name,
    string CamelCaseName,
    string TypeFullyQualified,
    string JsonSchemaType,
    string? JsonSchemaFormat,
    string? Description,
    bool IsNullable,
    bool IsRequired,
    bool IsValueType,
    string? DefaultValueLiteral,
    EquatableArray<string> EnumValues);
