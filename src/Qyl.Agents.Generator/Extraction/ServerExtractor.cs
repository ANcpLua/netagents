namespace Qyl.Agents.Generator.Extraction;

using Models;

internal static class ServerExtractor
{
    private const string McpServerAttributeName = "Qyl.Agents.McpServerAttribute";
    private const string ToolAttributeName = "Qyl.Agents.ToolAttribute";

    public static DiagnosticFlow<ServerModel> Extract(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol ||
            context.TargetNode is not ClassDeclarationSyntax classDeclaration)
            return DiagnosticFlow.Fail<ServerModel>(DiagnosticInfo.Create(
                DiagnosticDescriptors.ClassMustBePartial, context.TargetNode, context.TargetNode.ToString()));

        var guardFlow = SemanticGuard.ForType(typeSymbol)
            .MustBeClass(DiagnosticInfo.Create(DiagnosticDescriptors.ClassMustBePartial, typeSymbol, typeSymbol.Name))
            .MustNotBeStatic(DiagnosticInfo.Create(DiagnosticDescriptors.ClassMustNotBeStatic, typeSymbol,
                typeSymbol.Name))
            .MustNotBeGeneric(DiagnosticInfo.Create(DiagnosticDescriptors.ClassMustNotBeGeneric, typeSymbol,
                typeSymbol.Name))
            .ToFlow();

        var declarationsFlow = ExtractDeclarationChain(classDeclaration, cancellationToken);

        return DiagnosticFlow.Zip(guardFlow, declarationsFlow).Then(tuple =>
        {
            var (symbol, declarations) = tuple;
            var attr = symbol.GetAttribute(McpServerAttributeName);

            var serverName = attr?.GetConstructorArgument<string>(0)
                             ?? attr?.GetNamedArgument<string>("Name")
                             ?? symbol.Name.ToKebabCase();
            var description = attr?.GetNamedArgument<string>("Description")
                              ?? symbol.GetSummaryText(context.SemanticModel.Compilation, cancellationToken)
                              ?? string.Empty;
            var version = attr?.GetNamedArgument<string>("Version");

            var namespaceName = symbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : symbol.ContainingNamespace.ToDisplayString();

            return ExtractTools(symbol, context.SemanticModel.Compilation, cancellationToken)
                .Select(tools => new ServerModel(
                    namespaceName,
                    symbol.Name,
                    serverName,
                    description,
                    version,
                    declarations,
                    tools));
        });
    }

    private static DiagnosticFlow<EquatableArray<ToolModel>> ExtractTools(
        INamedTypeSymbol type,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var toolMethods = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary && m.HasAttribute(ToolAttributeName))
            .ToList();

        if (toolMethods.Count == 0)
        {
            var warning = DiagnosticInfo.Create(DiagnosticDescriptors.NoToolsFound, type, type.Name);
            return DiagnosticFlow.Ok(default(EquatableArray<ToolModel>)).Warn(warning);
        }

        var awaitable = new AwaitableContext(compilation);
        var toolFlows = toolMethods.Select(m => ToolExtractor.Extract(m, compilation, awaitable, cancellationToken));
        var collected = DiagnosticFlow.Collect(toolFlows);

        return collected.Then(tools =>
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var duplicateDiags = new List<DiagnosticInfo>();

            foreach (var tool in tools)
                if (!seen.Add(tool.ToolName))
                    duplicateDiags.Add(DiagnosticInfo.Create(
                        DiagnosticDescriptors.DuplicateToolName,
                        type,
                        tool.ToolName,
                        type.Name));

            if (duplicateDiags.Count > 0)
                return DiagnosticFlow.Fail<EquatableArray<ToolModel>>(duplicateDiags.ToArray());

            return tools.IsEmpty
                ? DiagnosticFlow.Ok(default(EquatableArray<ToolModel>))
                : DiagnosticFlow.Ok(tools.AsEquatableArray());
        });
    }

    private static DiagnosticFlow<EquatableArray<TypeDeclarationModel>> ExtractDeclarationChain(
        ClassDeclarationSyntax declaration,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<DiagnosticInfo>();
        var chain = new List<TypeDeclarationModel>();

        for (TypeDeclarationSyntax? current = declaration;
             current is not null;
             current = current.Parent as TypeDeclarationSyntax)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!current.Modifiers.Any(SyntaxKind.PartialKeyword))
                diagnostics.Add(DiagnosticInfo.Create(
                    DiagnosticDescriptors.ClassMustBePartial,
                    current.Identifier,
                    current.Identifier.ValueText));

            var modifiers = current.Modifiers.Select(static m => m.ValueText).ToList();
            if (!modifiers.Contains("partial"))
                modifiers.Add("partial");

            chain.Add(new TypeDeclarationModel(
                current.Identifier.ValueText,
                current.Keyword.ValueText,
                string.Join(" ", modifiers),
                current.TypeParameterList?.ToString().Trim() ?? string.Empty,
                current.ConstraintClauses.Count == 0
                    ? default
                    : current.ConstraintClauses.Select(static c => c.ToString().Trim()).ToArray().ToEquatableArray()));
        }

        chain.Reverse();

        if (diagnostics.Count > 0)
            return DiagnosticFlow.Fail<EquatableArray<TypeDeclarationModel>>(diagnostics.ToArray());

        return DiagnosticFlow.Ok(chain.Count is 0 ? default : chain.ToArray().ToEquatableArray());
    }
}
