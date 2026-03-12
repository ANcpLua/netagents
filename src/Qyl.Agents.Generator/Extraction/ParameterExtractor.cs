using Qyl.Agents.Generator.Models;

namespace Qyl.Agents.Generator.Extraction;

internal static class ParameterExtractor
{
    private const string DescriptionAttributeName = "System.ComponentModel.DescriptionAttribute";
    private const string CancellationTokenTypeName = "System.Threading.CancellationToken";

    public static DiagnosticFlow<EquatableArray<ToolParameterModel>> ExtractParameters(
        IMethodSymbol method,
        CancellationToken cancellationToken)
    {
        var parameters = new List<ToolParameterModel>();
        var diagnostics = new List<DiagnosticInfo>();

        foreach (var parameter in method.Parameters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Skip CancellationToken — handled separately
            if (IsCancellationToken(parameter.Type))
                continue;

            var (schemaType, schemaFormat, enumValues, isSupported) = MapToJsonSchema(parameter.Type);

            if (!isSupported)
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    DiagnosticDescriptors.UnsupportedParameterType,
                    parameter,
                    parameter.Name,
                    method.Name,
                    parameter.Type.ToDisplayString()));
                continue;
            }

            var description = parameter.GetAttributeConstructorArgument<string>(DescriptionAttributeName, 0);
            if (description is null)
                diagnostics.Add(DiagnosticInfo.Create(
                    DiagnosticDescriptors.ParameterMissingDescription,
                    parameter,
                    parameter.Name,
                    method.Name));

            var isNullable = IsNullable(parameter.Type);
            var hasDefault = parameter.HasExplicitDefaultValue;
            var defaultLiteral = hasDefault ? GetDefaultLiteral(parameter, cancellationToken) : null;
            var isRequired = !isNullable && !hasDefault;

            parameters.Add(new ToolParameterModel(
                parameter.Name,
                parameter.Name.ToParameterName(),
                parameter.Type.GetFullyQualifiedName(),
                schemaType,
                schemaFormat,
                description,
                isNullable,
                isRequired,
                parameter.Type.IsValueType,
                defaultLiteral,
                enumValues));
        }

        // Build flow with accumulated warnings (QA0009) but still succeeds
        var flow = DiagnosticFlow.Ok(
            parameters.Count is 0
                ? default
                : parameters.ToArray().ToEquatableArray());

        foreach (var diagnostic in diagnostics)
            // QA0009 is Warning, QA0008 is Error — errors cause failure
            if (diagnostic.Descriptor.DefaultSeverity == DiagnosticSeverity.Error)
                flow = flow.Error(diagnostic);
            else
                flow = flow.Warn(diagnostic);

        return flow;
    }

    private static bool IsCancellationToken(ITypeSymbol type)
    {
        return type.ToDisplayString() == CancellationTokenTypeName;
    }

    private static bool IsNullable(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { ConstructedFrom.SpecialType: SpecialType.System_Nullable_T })
            return true;
        return type.NullableAnnotation == NullableAnnotation.Annotated;
    }

    private static (string SchemaType, string? SchemaFormat, EquatableArray<string> EnumValues, bool IsSupported)
        MapToJsonSchema(ITypeSymbol type)
    {
        // Unwrap nullable<T>
        var coreType = type;
        if (type is INamedTypeSymbol { ConstructedFrom.SpecialType: SpecialType.System_Nullable_T } nullable)
            coreType = nullable.TypeArguments[0];

        // Arrays → array
        if (type is IArrayTypeSymbol)
            return ("array", null, default, true);

        // Named type dispatch
        if (coreType is INamedTypeSymbol named)
        {
            // Enum → string with values
            if (named.TypeKind == TypeKind.Enum)
            {
                var values = named.GetMembers()
                    .OfType<IFieldSymbol>()
                    .Where(static f => f.IsConst && f.HasConstantValue)
                    .Select(static f => f.Name)
                    .ToArray();
                return ("string", null, values.Length > 0 ? values.ToEquatableArray() : default, true);
            }

            switch (named.SpecialType)
            {
                case SpecialType.System_String:
                    return ("string", null, default, true);
                case SpecialType.System_Boolean:
                    return ("boolean", null, default, true);
                case SpecialType.System_Byte:
                case SpecialType.System_SByte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                    return ("integer", null, default, true);
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                    return ("number", null, default, true);
                case SpecialType.System_Decimal:
                    return ("number", null, default, true);
                case SpecialType.System_DateTime:
                    return ("string", "date-time", default, true);
            }

            var displayName = named.ToDisplayString();
            if (string.Equals(displayName, "System.DateTimeOffset", StringComparison.Ordinal))
                return ("string", "date-time", default, true);
            if (string.Equals(displayName, "System.Guid", StringComparison.Ordinal))
                return ("string", "uuid", default, true);
            if (string.Equals(displayName, "System.Uri", StringComparison.Ordinal))
                return ("string", "uri", default, true);

            // Object/complex type — supported as "object" at one level of depth
            if (named.TypeKind == TypeKind.Class || named.TypeKind == TypeKind.Struct)
                return ("object", null, default, true);
        }

        return ("string", null, default, false);
    }

    private static string? GetDefaultLiteral(IParameterSymbol parameter, CancellationToken cancellationToken)
    {
        var syntax = parameter.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken) as
            ParameterSyntax;
        var fromSyntax = syntax?.Default?.Value.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(fromSyntax))
            return fromSyntax;

        if (!parameter.HasExplicitDefaultValue) return null;
        var value = parameter.ExplicitDefaultValue;
        return value is null ? "null" : SymbolDisplay.FormatPrimitive(value, true, false);
    }
}