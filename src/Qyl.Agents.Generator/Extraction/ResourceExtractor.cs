namespace Qyl.Agents.Generator.Extraction;

using Models;

internal static class ResourceExtractor
{
    private const string ResourceAttributeName = "Qyl.Agents.ResourceAttribute";
    private const string CancellationTokenTypeName = "System.Threading.CancellationToken";

    public static DiagnosticFlow<ResourceModel> Extract(
        IMethodSymbol method,
        Compilation compilation,
        AwaitableContext awaitable,
        CancellationToken cancellationToken)
    {
        var guardFlow = SemanticGuard.ForMethod(method)
            .MustNotBeStatic(
                DiagnosticInfo.Create(DiagnosticDescriptors.ToolMethodMustNotBeStatic, method, method.Name))
            .Must(static m => !m.IsGenericMethod,
                DiagnosticInfo.Create(DiagnosticDescriptors.ToolMethodMustNotBeGeneric, method, method.Name))
            .ToFlow();

        if (guardFlow.IsFailed)
            return DiagnosticFlow.Fail<ResourceModel>(guardFlow.Diagnostics);

        var (returnKind, resultTypeFqn, isBinary) = ClassifyResourceReturnType(method, awaitable);

        if (returnKind is null)
            return DiagnosticFlow.Fail<ResourceModel>(DiagnosticInfo.Create(
                DiagnosticDescriptors.ResourceInvalidReturnType, method, method.Name,
                method.ReturnType.ToDisplayString()));

        var resourceAttr = method.GetAttribute(ResourceAttributeName);
        var uri = resourceAttr?.GetConstructorArgument<string>(0) ?? string.Empty;
        var name = resourceAttr?.GetNamedArgument<string>("Name");
        var description = resourceAttr?.GetNamedArgument<string>("Description")
                          ?? method.GetSummaryText(compilation, cancellationToken);
        var mimeType = resourceAttr?.GetNamedArgument<string>("MimeType");

        var hasCancellationToken = HasCancellationToken(method);

        return DiagnosticFlow.Ok(new ResourceModel(
            method.Name,
            uri,
            name,
            mimeType,
            description,
            resultTypeFqn,
            returnKind.Value,
            hasCancellationToken,
            isBinary));
    }

    private static (ReturnKind? Kind, string ResultFqn, bool IsBinary) ClassifyResourceReturnType(
        IMethodSymbol method, AwaitableContext awaitable)
    {
        // Sync string return
        if (method.ReturnType.SpecialType == SpecialType.System_String)
            return (ReturnKind.Sync, method.ReturnType.GetFullyQualifiedName(), false);

        // Sync byte[] return
        if (method.ReturnType is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte })
            return (ReturnKind.Sync, method.ReturnType.GetFullyQualifiedName(), true);

        var ret = method.ReturnType;

        if (awaitable.IsTaskLike(ret) && ret is INamedTypeSymbol namedRet)
        {
            var resultType = awaitable.GetTaskResultType(ret);
            if (resultType is not null)
            {
                var original = namedRet.OriginalDefinition.ToDisplayString();
                var isValueTask = original.StartsWith("System.Threading.Tasks.ValueTask", StringComparison.Ordinal);
                var kind = isValueTask ? ReturnKind.ValueTaskOfT : ReturnKind.TaskOfT;

                if (resultType.SpecialType == SpecialType.System_String)
                    return (kind, resultType.GetFullyQualifiedName(), false);

                if (resultType is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte })
                    return (kind, resultType.GetFullyQualifiedName(), true);
            }
        }

        return (null, string.Empty, false);
    }

    private static bool HasCancellationToken(IMethodSymbol method)
    {
        foreach (var p in method.Parameters)
            if (p.Type.ToDisplayString() == CancellationTokenTypeName)
                return true;
        return false;
    }
}
