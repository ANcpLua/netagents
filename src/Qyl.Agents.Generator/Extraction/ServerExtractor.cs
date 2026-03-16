namespace Qyl.Agents.Generator.Extraction;

using Models;

internal static class ServerExtractor
{
    private const string McpServerAttributeName = "Qyl.Agents.McpServerAttribute";
    private const string ToolAttributeName = "Qyl.Agents.ToolAttribute";
    private const string ResourceAttributeName = "Qyl.Agents.ResourceAttribute";
    private const string PromptAttributeName = "Qyl.Agents.PromptAttribute";

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

            var toolsFlow = ExtractTools(symbol, context.SemanticModel.Compilation, cancellationToken);
            var resourcesFlow = ExtractResources(symbol, context.SemanticModel.Compilation, cancellationToken);
            var promptsFlow = ExtractPrompts(symbol, context.SemanticModel.Compilation, cancellationToken);

            return DiagnosticFlow.Zip(toolsFlow, resourcesFlow).Then(tuple2 =>
            {
                var (tools, resources) = tuple2;
                return promptsFlow.Select(prompts => new ServerModel(
                    namespaceName,
                    symbol.Name,
                    serverName,
                    description,
                    version,
                    declarations,
                    tools,
                    resources,
                    prompts));
            });
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

    private static DiagnosticFlow<EquatableArray<ResourceModel>> ExtractResources(
        INamedTypeSymbol type,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var resourceMethods = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary && m.HasAttribute(ResourceAttributeName))
            .ToList();

        if (resourceMethods.Count == 0)
            return DiagnosticFlow.Ok(default(EquatableArray<ResourceModel>));

        var awaitable = new AwaitableContext(compilation);
        var resourceFlows = resourceMethods.Select(m =>
            ResourceExtractor.Extract(m, compilation, awaitable, cancellationToken));
        var collected = DiagnosticFlow.Collect(resourceFlows);

        return collected.Then(resources =>
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var duplicateDiags = new List<DiagnosticInfo>();

            foreach (var resource in resources)
                if (!seen.Add(resource.Uri))
                    duplicateDiags.Add(DiagnosticInfo.Create(
                        DiagnosticDescriptors.DuplicateResourceUri,
                        type,
                        resource.Uri,
                        type.Name));

            if (duplicateDiags.Count > 0)
                return DiagnosticFlow.Fail<EquatableArray<ResourceModel>>(duplicateDiags.ToArray());

            return resources.IsEmpty
                ? DiagnosticFlow.Ok(default(EquatableArray<ResourceModel>))
                : DiagnosticFlow.Ok(resources.AsEquatableArray());
        });
    }

    private static DiagnosticFlow<EquatableArray<PromptModel>> ExtractPrompts(
        INamedTypeSymbol type,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var promptMethods = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary && m.HasAttribute(PromptAttributeName))
            .ToList();

        if (promptMethods.Count == 0)
            return DiagnosticFlow.Ok(default(EquatableArray<PromptModel>));

        var awaitable = new AwaitableContext(compilation);
        var promptFlows = promptMethods.Select(m =>
            PromptExtractor.Extract(m, compilation, awaitable, cancellationToken));
        var collected = DiagnosticFlow.Collect(promptFlows);

        return collected.Then(prompts =>
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var duplicateDiags = new List<DiagnosticInfo>();

            foreach (var prompt in prompts)
                if (!seen.Add(prompt.PromptName))
                    duplicateDiags.Add(DiagnosticInfo.Create(
                        DiagnosticDescriptors.DuplicatePromptName,
                        type,
                        prompt.PromptName,
                        type.Name));

            if (duplicateDiags.Count > 0)
                return DiagnosticFlow.Fail<EquatableArray<PromptModel>>(duplicateDiags.ToArray());

            return prompts.IsEmpty
                ? DiagnosticFlow.Ok(default(EquatableArray<PromptModel>))
                : DiagnosticFlow.Ok(prompts.AsEquatableArray());
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
