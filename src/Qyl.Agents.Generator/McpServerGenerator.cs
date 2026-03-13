namespace Qyl.Agents.Generator;

using Extraction;
using Generation;

[Generator]
public sealed class McpServerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Primary pipeline: [McpServer] classes -> extract -> generate
        var serverFlows = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Qyl.Agents.McpServerAttribute",
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, ct) => ServerExtractor.Extract(ctx, ct));

        var servers = serverFlows.ReportAndStop(context);

        servers
            .Select(static (model, _) => OutputGenerator.GenerateOutput(model))
            .AddSource(context);

        // Secondary pipeline: orphaned [Tool] methods -> QA0004 diagnostic only
        var orphanedTools = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Qyl.Agents.ToolAttribute",
            static (node, _) => node is MethodDeclarationSyntax,
            static (ctx, _) =>
            {
                if (ctx.TargetSymbol is not IMethodSymbol method)
                    return default(DiagnosticInfo?);

                var containingType = method.ContainingType;
                if (containingType is null || containingType.HasAttribute("Qyl.Agents.McpServerAttribute"))
                    return null; // Not orphaned — inside an [McpServer] class

                return DiagnosticInfo.Create(
                    DiagnosticDescriptors.ToolMethodMustBeInsideMcpServer,
                    method,
                    method.Name);
            });

        context.RegisterSourceOutput(orphanedTools, static (ctx, diagnostic) =>
        {
            if (diagnostic is not null)
                ctx.ReportDiagnostic(diagnostic.Value.ToDiagnostic());
        });
    }
}
