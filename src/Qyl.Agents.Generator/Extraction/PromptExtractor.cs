namespace Qyl.Agents.Generator.Extraction;

using Models;

internal static class PromptExtractor
{
    private const string PromptAttributeName = "Qyl.Agents.PromptAttribute";
    private const string CancellationTokenTypeName = "System.Threading.CancellationToken";
    private const string PromptResultTypeName = "Qyl.Agents.PromptResult";

    public static DiagnosticFlow<PromptModel> Extract(
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
            return DiagnosticFlow.Fail<PromptModel>(guardFlow.Diagnostics);

        var (returnKind, resultTypeFqn, isStructured) = ClassifyPromptReturnType(method, awaitable);

        if (returnKind is null)
            return DiagnosticFlow.Fail<PromptModel>(DiagnosticInfo.Create(
                DiagnosticDescriptors.PromptInvalidReturnType, method, method.Name,
                method.ReturnType.ToDisplayString()));

        var promptAttr = method.GetAttribute(PromptAttributeName);
        var promptName = promptAttr?.GetConstructorArgument<string>(0)
                         ?? promptAttr?.GetNamedArgument<string>("Name")
                         ?? method.Name.ToKebabCase();
        var description = promptAttr?.GetNamedArgument<string>("Description")
                          ?? method.GetSummaryText(compilation, cancellationToken)
                          ?? string.Empty;

        var hasCancellationToken = HasCancellationToken(method);

        return ParameterExtractor.ExtractParameters(method, cancellationToken)
            .Select(parameters => new PromptModel(
                method.Name,
                promptName,
                description,
                resultTypeFqn,
                returnKind.Value,
                hasCancellationToken,
                isStructured,
                parameters));
    }

    private static (ReturnKind? Kind, string ResultFqn, bool IsStructured) ClassifyPromptReturnType(
        IMethodSymbol method, AwaitableContext awaitable)
    {
        // Sync string return
        if (method.ReturnType.SpecialType == SpecialType.System_String)
            return (ReturnKind.Sync, method.ReturnType.GetFullyQualifiedName(), false);

        // Sync PromptResult return
        if (method.ReturnType.ToDisplayString() == PromptResultTypeName)
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
                var resultFqn = resultType.ToDisplayString();

                if (resultType.SpecialType == SpecialType.System_String)
                    return (kind, resultType.GetFullyQualifiedName(), false);

                if (resultFqn == PromptResultTypeName)
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
