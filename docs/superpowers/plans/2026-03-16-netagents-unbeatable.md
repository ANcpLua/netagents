# netagents: MCP Server Generator Enhancements — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make netagents-generated MCP servers discoverable, submittable to the Anthropic directory, and deployable over HTTP — all from `[McpServer]` + attributes at compile time.

**Architecture:** Five additive sections extending the existing IIncrementalGenerator pipeline. Each section adds new attributes to Abstractions, new extractor/emitter pairs to the Generator, and new protocol handlers to the Runtime. Section 5 adds a new `Qyl.Agents.Http` package.

**Tech Stack:** C# 14, .NET 10, Roslyn IIncrementalGenerator, xUnit v3, ANcpLua.Roslyn.Utilities.Testing

**Spec:** `docs/superpowers/specs/2026-03-16-netagents-unbeatable-design.md`

---

## File Map

### New Files

| File | Responsibility |
|------|---------------|
| `src/Qyl.Agents.Abstractions/ToolHint.cs` | Tri-state enum: Unset/True/False |
| `src/Qyl.Agents.Abstractions/ResourceAttribute.cs` | `[Resource]` marker attribute |
| `src/Qyl.Agents.Abstractions/McpResourceInfo.cs` | Resource runtime descriptor |
| `src/Qyl.Agents.Abstractions/ResourceReadResult.cs` | Text/blob result wrapper |
| `src/Qyl.Agents.Abstractions/PromptAttribute.cs` | `[Prompt]` marker attribute |
| `src/Qyl.Agents.Abstractions/PromptRole.cs` | String constants for roles |
| `src/Qyl.Agents.Abstractions/PromptMessage.cs` | Single prompt message record |
| `src/Qyl.Agents.Abstractions/PromptResult.cs` | Structured prompt result |
| `src/Qyl.Agents.Abstractions/McpPromptArgument.cs` | Prompt argument descriptor |
| `src/Qyl.Agents.Abstractions/McpPromptInfo.cs` | Prompt runtime descriptor |
| `src/Qyl.Agents.Generator/Models/ResourceModel.cs` | Generator-internal resource model |
| `src/Qyl.Agents.Generator/Models/PromptModel.cs` | Generator-internal prompt model |
| `src/Qyl.Agents.Generator/Extraction/ResourceExtractor.cs` | Extract `[Resource]` methods |
| `src/Qyl.Agents.Generator/Extraction/PromptExtractor.cs` | Extract `[Prompt]` methods |
| `src/Qyl.Agents.Generator/Generation/ResourceDispatchEmitter.cs` | Emit resource dispatch + metadata |
| `src/Qyl.Agents.Generator/Generation/PromptDispatchEmitter.cs` | Emit prompt dispatch + metadata |
| `src/Qyl.Agents.Generator/Generation/LlmsEmitter.cs` | Emit `s_llmsTxt` const string |
| `src/Qyl.Agents.Http/Qyl.Agents.Http.csproj` | HTTP transport package |
| `src/Qyl.Agents.Http/McpHttpHostExtensions.cs` | `MapMcpServer<T>()` extension |

### Modified Files

| File | Changes |
|------|---------|
| `src/Qyl.Agents.Abstractions/ToolAttribute.cs` | Add 4 `ToolHint` properties |
| `src/Qyl.Agents.Abstractions/McpToolInfo.cs` | Add 4 `ToolHint` annotation fields |
| `src/Qyl.Agents/IMcpServer.cs` | Add resource/prompt/llmstxt members |
| `src/Qyl.Agents/Protocol/McpProtocolHandler.cs` | Add resources/prompts handlers, annotations in tools/list |
| `src/Qyl.Agents/Hosting/McpHost.cs` | Add `WriteLlmsTxtAsync` |
| `src/Qyl.Agents/Qyl.Agents.csproj` | Add InternalsVisibleTo for Http |
| `src/Qyl.Agents.Generator/Models/ToolModel.cs` | Add 4 `ToolHint` fields |
| `src/Qyl.Agents.Generator/Models/ServerModel.cs` | Add Resources + Prompts collections |
| `src/Qyl.Agents.Generator/Extraction/ToolExtractor.cs` | Read annotation values + QA0012 |
| `src/Qyl.Agents.Generator/Extraction/ServerExtractor.cs` | Extract resources + prompts |
| `src/Qyl.Agents.Generator/Generation/MetadataEmitter.cs` | Emit annotations, resource/prompt infos |
| `src/Qyl.Agents.Generator/Generation/SkillEmitter.cs` | Add Resources + Prompts sections |
| `src/Qyl.Agents.Generator/Generation/OutputGenerator.cs` | Wire new emitters |
| `src/Qyl.Agents.Generator/DiagnosticDescriptors.cs` | QA0012-QA0016 |
| `tests/Qyl.Agents.Generator.Tests/McpServerGeneratorTests.cs` | All new tests |
| `netagents.slnx` | Add Qyl.Agents.Http project |

---

## Chunk 1: Safety Annotations (Section 1)

### Task 1: Add `ToolHint` enum and update `ToolAttribute`

**Files:**
- Create: `src/Qyl.Agents.Abstractions/ToolHint.cs`
- Modify: `src/Qyl.Agents.Abstractions/ToolAttribute.cs`

- [ ] **Step 1: Create `ToolHint.cs`**

```csharp
namespace Qyl.Agents;

/// <summary>Tri-state hint for MCP tool safety annotations.</summary>
public enum ToolHint : byte
{
    /// <summary>Developer did not declare — unknown to agent.</summary>
    Unset = 0,
    /// <summary>Developer explicitly declared true.</summary>
    True = 1,
    /// <summary>Developer explicitly declared false.</summary>
    False = 2
}
```

- [ ] **Step 2: Add annotation properties to `ToolAttribute`**

In `src/Qyl.Agents.Abstractions/ToolAttribute.cs`, add after the `Description` property:

```csharp
/// <summary>Hint: tool performs read-only operations.</summary>
public ToolHint ReadOnly { get; set; }

/// <summary>Hint: tool performs destructive operations.</summary>
public ToolHint Destructive { get; set; }

/// <summary>Hint: tool is idempotent (safe to retry).</summary>
public ToolHint Idempotent { get; set; }

/// <summary>Hint: tool interacts with the open world (network, external APIs).</summary>
public ToolHint OpenWorld { get; set; }
```

- [ ] **Step 3: Build to verify compilation**

Run: `dotnet build src/Qyl.Agents.Abstractions/Qyl.Agents.Abstractions.csproj`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```
feat(abstractions): add ToolHint enum and safety annotation properties to [Tool]
```

### Task 2: Add `ToolHint` fields to `McpToolInfo` and `ToolModel`

**Files:**
- Modify: `src/Qyl.Agents.Abstractions/McpToolInfo.cs`
- Modify: `src/Qyl.Agents.Generator/Models/ToolModel.cs`

- [ ] **Step 1: Add annotation fields to `McpToolInfo`**

In `src/Qyl.Agents.Abstractions/McpToolInfo.cs`, add after `InputSchema`:

```csharp
/// <summary>Safety annotation: read-only hint.</summary>
public ToolHint ReadOnly { get; init; }

/// <summary>Safety annotation: destructive hint.</summary>
public ToolHint Destructive { get; init; }

/// <summary>Safety annotation: idempotent hint.</summary>
public ToolHint Idempotent { get; init; }

/// <summary>Safety annotation: open-world hint.</summary>
public ToolHint OpenWorld { get; init; }
```

- [ ] **Step 2: Add annotation fields to `ToolModel`**

Replace `src/Qyl.Agents.Generator/Models/ToolModel.cs`:

```csharp
namespace Qyl.Agents.Generator.Models;

internal enum ToolHintValue : byte { Unset = 0, True = 1, False = 2 }

internal readonly record struct ToolModel(
    string MethodName,
    string ToolName,
    string Description,
    string ResultTypeFullyQualified,
    ReturnKind ReturnKind,
    bool HasCancellationToken,
    EquatableArray<ToolParameterModel> Parameters,
    ToolHintValue ReadOnly,
    ToolHintValue Destructive,
    ToolHintValue Idempotent,
    ToolHintValue OpenWorld);
```

Note: Generator uses its own `ToolHintValue` enum mirroring the Abstractions `ToolHint` by value (byte 0/1/2). Same pattern as `ReturnKind` — generator-internal enum, not the public type.

- [ ] **Step 3: Build the generator project**

Run: `dotnet build src/Qyl.Agents.Generator/Qyl.Agents.Generator.csproj`
Expected: Build errors in `ToolExtractor.cs` (missing constructor arguments) — expected, fixed in Task 3.

- [ ] **Step 4: Commit**

```
feat(models): add safety annotation fields to ToolModel and McpToolInfo
```

### Task 3: Extract annotations in `ToolExtractor` and add QA0012

**Files:**
- Modify: `src/Qyl.Agents.Generator/Extraction/ToolExtractor.cs`
- Modify: `src/Qyl.Agents.Generator/DiagnosticDescriptors.cs`

- [ ] **Step 1: Add QA0012 diagnostic descriptor**

In `DiagnosticDescriptors.cs`, add after `DuplicateToolName`:

```csharp
public static readonly DiagnosticDescriptor ToolMissingSafetyAnnotations = new(
    "QA0012",
    "Tool has no safety annotations",
    "Tool method '{0}' has no safety annotations (ReadOnly, Destructive, Idempotent, OpenWorld)",
    Category,
    DiagnosticSeverity.Warning,
    true);
```

- [ ] **Step 2: Read annotations in `ToolExtractor.Extract()`**

After reading `toolName` and `description`, add:

```csharp
var readOnly = (ToolHintValue)(toolAttr?.GetNamedArgument<int>("ReadOnly") ?? 0);
var destructive = (ToolHintValue)(toolAttr?.GetNamedArgument<int>("Destructive") ?? 0);
var idempotent = (ToolHintValue)(toolAttr?.GetNamedArgument<int>("Idempotent") ?? 0);
var openWorld = (ToolHintValue)(toolAttr?.GetNamedArgument<int>("OpenWorld") ?? 0);
```

Update the `ToolModel` constructor call to pass all four values.

- [ ] **Step 3: Emit QA0012 when all annotations are Unset**

After creating the `ToolModel`, check if all four are 0 (Unset) and add a warning diagnostic:

```csharp
var flow = ParameterExtractor.ExtractParameters(method, cancellationToken)
    .Select(parameters => new ToolModel(
        method.Name, toolName, description, resultTypeFqn,
        returnKind.Value, hasCancellationToken, parameters,
        readOnly, destructive, idempotent, openWorld));

if (readOnly == ToolHintValue.Unset && destructive == ToolHintValue.Unset && idempotent == ToolHintValue.Unset && openWorld == ToolHintValue.Unset)
    flow = flow.Warn(DiagnosticInfo.Create(
        DiagnosticDescriptors.ToolMissingSafetyAnnotations, method, method.Name));

return flow;
```

- [ ] **Step 4: Build the generator**

Run: `dotnet build src/Qyl.Agents.Generator/Qyl.Agents.Generator.csproj`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```
feat(extraction): read safety annotations from [Tool] and emit QA0012 warning
```

### Task 4: Emit annotations in `MetadataEmitter` and `McpProtocolHandler`

**Files:**
- Modify: `src/Qyl.Agents.Generator/Generation/MetadataEmitter.cs`
- Modify: `src/Qyl.Agents/Protocol/McpProtocolHandler.cs`

- [ ] **Step 1: Emit annotation values in `MetadataEmitter.Emit()`**

In the `GetToolInfos()` method, add after the `InputSchema` line:

```csharp
if (tool.ReadOnly != ToolHintValue.Unset)
    sb.AppendLine($"ReadOnly = (global::Qyl.Agents.ToolHint){(byte)tool.ReadOnly},");
if (tool.Destructive != ToolHintValue.Unset)
    sb.AppendLine($"Destructive = (global::Qyl.Agents.ToolHint){(byte)tool.Destructive},");
if (tool.Idempotent != ToolHintValue.Unset)
    sb.AppendLine($"Idempotent = (global::Qyl.Agents.ToolHint){(byte)tool.Idempotent},");
if (tool.OpenWorld != ToolHintValue.Unset)
    sb.AppendLine($"OpenWorld = (global::Qyl.Agents.ToolHint){(byte)tool.OpenWorld},");
```

- [ ] **Step 2: Write annotations in `McpProtocolHandler.BuildToolsListResult()`**

After writing `inputSchema` for each tool, add:

```csharp
var t = s_tools[i];
if (t.ReadOnly != ToolHint.Unset || t.Destructive != ToolHint.Unset ||
    t.Idempotent != ToolHint.Unset || t.OpenWorld != ToolHint.Unset)
{
    w.WriteStartObject("annotations");
    if (t.ReadOnly != ToolHint.Unset)
        w.WriteBoolean("readOnlyHint", t.ReadOnly == ToolHint.True);
    if (t.Destructive != ToolHint.Unset)
        w.WriteBoolean("destructiveHint", t.Destructive == ToolHint.True);
    if (t.Idempotent != ToolHint.Unset)
        w.WriteBoolean("idempotentHint", t.Idempotent == ToolHint.True);
    if (t.OpenWorld != ToolHint.Unset)
        w.WriteBoolean("openWorldHint", t.OpenWorld == ToolHint.True);
    w.WriteEndObject();
}
```

- [ ] **Step 3: Update `SkillEmitter` to include annotation hints**

In `SkillEmitter.BuildSkillMdContent()`, after writing the tool description, add annotation hints:

```csharp
var hints = new List<string>();
if (tool.ReadOnly != ToolHintValue.Unset) hints.Add(tool.ReadOnly == ToolHintValue.True ? "read-only" : "not read-only");
if (tool.Destructive != ToolHintValue.Unset) hints.Add(tool.Destructive == ToolHintValue.True ? "destructive" : "not destructive");
if (tool.Idempotent != ToolHintValue.Unset) hints.Add(tool.Idempotent == ToolHintValue.True ? "idempotent" : "not idempotent");
if (tool.OpenWorld != ToolHintValue.Unset) hints.Add(tool.OpenWorld == ToolHintValue.True ? "open-world" : "not open-world");
if (hints.Count > 0)
    md.Append("*Annotations: ").Append(string.Join(", ", hints)).AppendLine("*");
```

- [ ] **Step 4: Build everything**

Run: `dotnet build`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```
feat(generation): emit safety annotations in tools/list, SKILL.md, and MetadataEmitter
```

### Task 5: Write tests for safety annotations

**Files:**
- Modify: `tests/Qyl.Agents.Generator.Tests/McpServerGeneratorTests.cs`

- [ ] **Step 1: Write test for hint serialization**

```csharp
[Fact]
public async Task SafetyAnnotationsEmittedInGeneratedCode()
{
    var source = """
                 using Qyl.Agents;

                 namespace AnnotTest;

                 /// <summary>Test</summary>
                 [McpServer]
                 public partial class AnnotServer
                 {
                     /// <summary>List items</summary>
                     [Tool(ReadOnly = ToolHint.True, Idempotent = ToolHint.True)]
                     public string List(string path) => path;
                 }
                 """;

    using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
    result
        .File("AnnotTest.AnnotServer.McpServer.g.cs", content =>
        {
            Assert.Contains("ReadOnly = (global::Qyl.Agents.ToolHint)1", content);
            Assert.Contains("Idempotent = (global::Qyl.Agents.ToolHint)1", content);
            Assert.DoesNotContain("Destructive", content);
            Assert.DoesNotContain("OpenWorld", content);
        })
        .Compiles();
}
```

- [ ] **Step 2: Write test for QA0012 warning**

```csharp
[Fact]
public async Task MissingSafetyAnnotationsReportsQA0012()
{
    var source = """
                 using Qyl.Agents;

                 /// <summary>Test</summary>
                 [McpServer]
                 public partial class NoAnnotServer
                 {
                     /// <summary>Echo</summary>
                     [Tool]
                     public string Echo(string input) => input;
                 }
                 """;

    using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
    result
        .HasDiagnostic("QA0012", DiagnosticSeverity.Warning)
        .Compiles();
}
```

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/Qyl.Agents.Generator.Tests/Qyl.Agents.Generator.Tests.csproj`
Expected: All tests pass (including existing tests — QA0012 will fire on existing test sources, but they use `.Compiles()` not `.NoDiagnostics()`)

**Important:** Existing tests that use `[Tool]` without annotations will now get QA0012 warnings. Since QA0012 is a Warning (not Error), `.Compiles()` still passes. Verify no existing test breaks.

- [ ] **Step 4: Commit**

```
test: add safety annotation serialization and QA0012 diagnostic tests
```

---

## Chunk 2: Resources (Section 2)

### Task 6: Add `[Resource]` attribute and related types to Abstractions

**Files:**
- Create: `src/Qyl.Agents.Abstractions/ResourceAttribute.cs`
- Create: `src/Qyl.Agents.Abstractions/McpResourceInfo.cs`
- Create: `src/Qyl.Agents.Abstractions/ResourceReadResult.cs`

- [ ] **Step 1: Create `ResourceAttribute.cs`**

```csharp
namespace Qyl.Agents;

/// <summary>
///     Marks a method as an MCP resource within an <see cref="McpServerAttribute" /> class.
///     The source generator will produce resource dispatch code and metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ResourceAttribute(string uri) : Attribute
{
    /// <summary>Resource URI for MCP protocol (e.g. "config://agents.toml").</summary>
    public string Uri { get; } = uri;

    /// <summary>MIME type of the resource content (e.g. "application/toml").</summary>
    public string? MimeType { get; set; }

    /// <summary>Human-readable description. Defaults to XML doc summary on the method.</summary>
    public string? Description { get; set; }
}
```

- [ ] **Step 2: Create `McpResourceInfo.cs`**

```csharp
namespace Qyl.Agents;

/// <summary>
///     Describes a single MCP resource. Returned by the generated <c>GetResourceInfos()</c> method.
/// </summary>
public sealed class McpResourceInfo
{
    /// <summary>Resource URI as advertised in the MCP <c>resources/list</c> response.</summary>
    public required string Uri { get; init; }

    /// <summary>MIME type of the resource content.</summary>
    public string? MimeType { get; init; }

    /// <summary>Human-readable description of the resource.</summary>
    public string? Description { get; init; }
}
```

- [ ] **Step 3: Create `ResourceReadResult.cs`**

```csharp
namespace Qyl.Agents;

using System.Collections.Generic;

/// <summary>
///     Result of reading an MCP resource. Carries the content and a flag indicating
///     whether the content is base64-encoded binary (<c>IsBinary = true</c>) or plain text.
/// </summary>
public sealed class ResourceReadResult
{
    public ResourceReadResult(string content, bool isBinary)
    {
        Content = content;
        IsBinary = isBinary;
    }

    /// <summary>Resource content (plain text or base64-encoded binary).</summary>
    public string Content { get; }

    /// <summary>True if content is base64-encoded binary; false if plain text.</summary>
    public bool IsBinary { get; }
}
```

Note: Using a class with constructor instead of `record` for netstandard2.0 consistency with `McpToolInfo`/`McpServerInfo` pattern. The `record` keyword compiles on netstandard2.0 with the `IsExternalInit` polyfill, but using a class matches the existing convention.

- [ ] **Step 4: Build**

Run: `dotnet build src/Qyl.Agents.Abstractions/Qyl.Agents.Abstractions.csproj`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```
feat(abstractions): add [Resource] attribute, McpResourceInfo, and ResourceReadResult
```

### Task 7: Add `ResourceModel` and `ResourceExtractor`

**Files:**
- Create: `src/Qyl.Agents.Generator/Models/ResourceModel.cs`
- Create: `src/Qyl.Agents.Generator/Extraction/ResourceExtractor.cs`
- Modify: `src/Qyl.Agents.Generator/DiagnosticDescriptors.cs`

- [ ] **Step 1: Create `ResourceModel.cs`**

```csharp
namespace Qyl.Agents.Generator.Models;

internal readonly record struct ResourceModel(
    string MethodName,
    string Uri,
    string? MimeType,
    string? Description,
    string ResultTypeFullyQualified,
    ReturnKind ReturnKind,
    bool HasCancellationToken,
    bool IsBinary);
```

- [ ] **Step 2: Add QA0013 and QA0014 diagnostics**

In `DiagnosticDescriptors.cs`:

```csharp
public static readonly DiagnosticDescriptor ResourceUnsupportedReturnType = new(
    "QA0013",
    "Resource method has unsupported return type",
    "Method '{0}' decorated with [Resource] has unsupported return type '{1}' — supported types are string, byte[], Task<string>, Task<byte[]>, ValueTask<string>, ValueTask<byte[]>",
    Category,
    DiagnosticSeverity.Error,
    true);

public static readonly DiagnosticDescriptor DuplicateResourceUri = new(
    "QA0014",
    "Duplicate resource URI",
    "Resource URI '{0}' is used by multiple methods in class '{1}'",
    Category,
    DiagnosticSeverity.Error,
    true);
```

- [ ] **Step 3: Create `ResourceExtractor.cs`**

Follow the `ToolExtractor` pattern exactly.

Note: The static/generic method guard diagnostics (QA0005/QA0006) are intentionally reused from the tool descriptors. The message text says "decorated with [Tool]" but the diagnostic correctly points to the offending method regardless. Adding separate QA codes for resource/prompt static/generic guards is not worth the descriptor proliferation — the developer sees the error on the right method and understands the constraint.

```csharp
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
        // Reuse tool guard diagnostics — message text references [Tool] but location is correct
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
                DiagnosticDescriptors.ResourceUnsupportedReturnType, method, method.Name,
                method.ReturnType.ToDisplayString()));

        var attr = method.GetAttribute(ResourceAttributeName);
        var uri = attr?.GetConstructorArgument<string>(0) ?? "";
        var mimeType = attr?.GetNamedArgument<string>("MimeType");
        var description = attr?.GetNamedArgument<string>("Description")
                          ?? method.GetSummaryText(compilation, cancellationToken)
                          ?? string.Empty;

        var hasCancellationToken = method.Parameters.Any(
            p => p.Type.ToDisplayString() == CancellationTokenTypeName);

        return DiagnosticFlow.Ok(new ResourceModel(
            method.Name, uri, mimeType, description,
            resultTypeFqn, returnKind.Value, hasCancellationToken, isBinary));
    }

    private static (ReturnKind? Kind, string ResultFqn, bool IsBinary) ClassifyResourceReturnType(
        IMethodSymbol method, AwaitableContext awaitable)
    {
        // Unwrap async wrappers
        var returnType = method.ReturnType;
        var returnKind = ReturnKind.Sync;

        if (awaitable.IsTaskLike(returnType))
        {
            var resultType = awaitable.GetTaskResultType(returnType);
            if (resultType is null) return (null, string.Empty, false); // Task/ValueTask without result
            returnType = resultType;

            var original = ((INamedTypeSymbol)method.ReturnType).OriginalDefinition.ToDisplayString();
            returnKind = original.StartsWith("System.Threading.Tasks.ValueTask", StringComparison.Ordinal)
                ? ReturnKind.ValueTaskOfT
                : ReturnKind.TaskOfT;
        }

        // Check if unwrapped type is string or byte[]
        var display = returnType.ToDisplayString();
        if (display is "string" || returnType.SpecialType == SpecialType.System_String)
            return (returnKind == ReturnKind.Sync ? ReturnKind.Sync : returnKind,
                returnType.GetFullyQualifiedName(), false);

        if (returnType is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte })
            return (returnKind == ReturnKind.Sync ? ReturnKind.Sync : returnKind,
                returnType.GetFullyQualifiedName(), true);

        return (null, string.Empty, false);
    }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build src/Qyl.Agents.Generator/Qyl.Agents.Generator.csproj`
Expected: Build succeeded (ResourceExtractor not yet wired into ServerExtractor)

- [ ] **Step 5: Commit**

```
feat(extraction): add ResourceExtractor with QA0013/QA0014 diagnostics
```

### Task 8: Wire resources into `ServerExtractor` and `ServerModel`

**Files:**
- Modify: `src/Qyl.Agents.Generator/Models/ServerModel.cs`
- Modify: `src/Qyl.Agents.Generator/Extraction/ServerExtractor.cs`

- [ ] **Step 1: Add Resources to `ServerModel`**

```csharp
internal readonly record struct ServerModel(
    string Namespace,
    string ClassName,
    string ServerName,
    string Description,
    string? Version,
    EquatableArray<TypeDeclarationModel> DeclarationChain,
    EquatableArray<ToolModel> Tools,
    EquatableArray<ResourceModel> Resources);
```

- [ ] **Step 2: Extract resources in `ServerExtractor`**

Add `ResourceAttributeName` constant:

```csharp
private const string ResourceAttributeName = "Qyl.Agents.ResourceAttribute";
```

Add `ExtractResources` method (same pattern as `ExtractTools`):

```csharp
private static DiagnosticFlow<EquatableArray<ResourceModel>> ExtractResources(
    INamedTypeSymbol type, Compilation compilation, CancellationToken cancellationToken)
{
    var resourceMethods = type.GetMembers()
        .OfType<IMethodSymbol>()
        .Where(m => m.MethodKind == MethodKind.Ordinary && m.HasAttribute(ResourceAttributeName))
        .ToList();

    if (resourceMethods.Count == 0)
        return DiagnosticFlow.Ok(default(EquatableArray<ResourceModel>));

    var awaitable = new AwaitableContext(compilation);
    var flows = resourceMethods.Select(m => ResourceExtractor.Extract(m, compilation, awaitable, cancellationToken));
    var collected = DiagnosticFlow.Collect(flows);

    return collected.Then(resources =>
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = new List<DiagnosticInfo>();
        foreach (var r in resources)
            if (!seen.Add(r.Uri))
                duplicates.Add(DiagnosticInfo.Create(
                    DiagnosticDescriptors.DuplicateResourceUri, type, r.Uri, type.Name));

        if (duplicates.Count > 0)
            return DiagnosticFlow.Fail<EquatableArray<ResourceModel>>(duplicates.ToArray());

        return resources.IsEmpty
            ? DiagnosticFlow.Ok(default(EquatableArray<ResourceModel>))
            : DiagnosticFlow.Ok(resources.AsEquatableArray());
    });
}
```

Wire into `Extract()` method — zip with tools:

```csharp
return DiagnosticFlow.Zip(guardFlow, declarationsFlow).Then(tuple =>
{
    var (symbol, declarations) = tuple;
    // ... existing attr reading ...

    return ExtractTools(symbol, compilation, cancellationToken).Then(tools =>
        ExtractResources(symbol, compilation, cancellationToken).Select(resources =>
            new ServerModel(namespaceName, symbol.Name, serverName, description, version,
                declarations, tools, resources)));
});
```

- [ ] **Step 3: Fix all `ServerModel` constructor calls**

Update `OutputGenerator.GenerateOutput()` and any tests that construct `ServerModel` directly.

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```
feat(extraction): wire ResourceExtractor into ServerExtractor pipeline
```

### Task 9: Add `ResourceDispatchEmitter` and update `MetadataEmitter`

**Files:**
- Create: `src/Qyl.Agents.Generator/Generation/ResourceDispatchEmitter.cs`
- Modify: `src/Qyl.Agents.Generator/Generation/MetadataEmitter.cs`
- Modify: `src/Qyl.Agents.Generator/Generation/OutputGenerator.cs`
- Modify: `src/Qyl.Agents.Generator/Generation/SkillEmitter.cs`

- [ ] **Step 1: Create `ResourceDispatchEmitter.cs`**

Emit `DispatchResourceReadAsync` and `GetResourceInfos`:

```csharp
namespace Qyl.Agents.Generator.Generation;

using Models;

internal static class ResourceDispatchEmitter
{
    public static void Emit(IndentedStringBuilder sb, ServerModel server)
    {
        EmitDispatch(sb, server);
        sb.AppendLine();
        EmitMetadata(sb, server);
    }

    private static void EmitDispatch(IndentedStringBuilder sb, ServerModel server)
    {
        sb.AppendLine("public async global::System.Threading.Tasks.Task<global::Qyl.Agents.ResourceReadResult> DispatchResourceReadAsync(");
        sb.AppendLine("    string uri,");
        sb.AppendLine("    global::System.Threading.CancellationToken cancellationToken = default)");
        using (sb.BeginBlock())
        {
            sb.AppendLine("return uri switch");
            using (sb.BeginBlock())
            {
                foreach (var r in server.Resources)
                    sb.AppendLine($"{EmitHelpers.Lit(r.Uri)} => await ExecuteResource_{r.MethodName}Async(cancellationToken),");
                sb.AppendLine("_ => throw new global::System.ArgumentException($\"Unknown resource: {uri}\", nameof(uri))");
            }
            sb.AppendLine(";");
        }
        sb.AppendLine();

        // Per-resource methods
        foreach (var r in server.Resources)
            EmitPerResourceMethod(sb, r);
    }

    private static void EmitPerResourceMethod(IndentedStringBuilder sb, ResourceModel resource)
    {
        var isAsync = resource.ReturnKind is ReturnKind.TaskOfT or ReturnKind.ValueTaskOfT;
        var asyncKeyword = isAsync ? "async " : "";
        sb.AppendLine($"private {asyncKeyword}global::System.Threading.Tasks.Task<global::Qyl.Agents.ResourceReadResult> ExecuteResource_{resource.MethodName}Async(");
        sb.AppendLine("    global::System.Threading.CancellationToken cancellationToken)");
        using (sb.BeginBlock())
        {
            var callArgs = resource.HasCancellationToken ? "cancellationToken" : "";
            var awaitPrefix = isAsync ? "await " : "";

            if (resource.IsBinary)
            {
                sb.AppendLine($"var raw = {awaitPrefix}{resource.MethodName}({callArgs});");
                sb.AppendLine("return new global::Qyl.Agents.ResourceReadResult(global::System.Convert.ToBase64String(raw), true);");
            }
            else
            {
                sb.AppendLine($"var raw = {awaitPrefix}{resource.MethodName}({callArgs});");
                sb.AppendLine("return new global::Qyl.Agents.ResourceReadResult(raw, false);");
            }
        }
        sb.AppendLine();
    }

    private static void EmitMetadata(IndentedStringBuilder sb, ServerModel server)
    {
        sb.AppendLine("public static global::System.Collections.Generic.IReadOnlyList<global::Qyl.Agents.McpResourceInfo> GetResourceInfos()");
        using (sb.BeginBlock())
        {
            if (server.Resources.IsEmpty)
            {
                sb.AppendLine("return global::System.Array.Empty<global::Qyl.Agents.McpResourceInfo>();");
            }
            else
            {
                sb.AppendLine("return new global::Qyl.Agents.McpResourceInfo[]");
                using (sb.BeginBlock())
                {
                    foreach (var r in server.Resources)
                    {
                        sb.AppendLine("new global::Qyl.Agents.McpResourceInfo");
                        using (sb.BeginBlock())
                        {
                            sb.AppendLine($"Uri = {EmitHelpers.Lit(r.Uri)},");
                            if (r.MimeType is not null)
                                sb.AppendLine($"MimeType = {EmitHelpers.Lit(r.MimeType)},");
                            if (r.Description is not null)
                                sb.AppendLine($"Description = {EmitHelpers.Lit(r.Description)},");
                        }
                        sb.AppendLine(",");
                    }
                }
                sb.AppendLine(";");
            }
        }
    }
}
```

- [ ] **Step 2: Wire into `OutputGenerator`**

After `DispatchEmitter.Emit(sb, server);`, add:

```csharp
ResourceDispatchEmitter.Emit(sb, server);
```

- [ ] **Step 3: Add `## Resources` section to `SkillEmitter`**

In `BuildSkillMdContent`, after the tools loop:

```csharp
if (!server.Resources.IsEmpty)
{
    md.AppendLine("## Resources");
    md.AppendLine();
    foreach (var r in server.Resources)
    {
        md.Append("### ").AppendLine(r.Uri);
        md.AppendLine();
        if (!string.IsNullOrEmpty(r.Description))
            md.AppendLine(r.Description);
        if (r.MimeType is not null)
            md.Append("MIME type: ").AppendLine(r.MimeType);
        md.AppendLine();
    }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: Build errors in `IMcpServer.cs` — expected, fixed in Task 10

- [ ] **Step 5: Commit**

```
feat(generation): add ResourceDispatchEmitter with dispatch and metadata generation
```

### Task 10: Update `IMcpServer` and `McpProtocolHandler` for resources

**Files:**
- Modify: `src/Qyl.Agents/IMcpServer.cs`
- Modify: `src/Qyl.Agents/Protocol/McpProtocolHandler.cs`

- [ ] **Step 1: Add resource members to `IMcpServer`**

```csharp
/// <summary>Returns all resource descriptors for the MCP <c>resources/list</c> response.</summary>
static abstract IReadOnlyList<McpResourceInfo> GetResourceInfos();

/// <summary>Dispatches a resource read by URI, returning content and binary flag.</summary>
Task<ResourceReadResult> DispatchResourceReadAsync(
    string uri,
    CancellationToken cancellationToken = default);
```

- [ ] **Step 2: Add resource handlers to `McpProtocolHandler`**

Cache resource infos:

```csharp
private static readonly IReadOnlyList<McpResourceInfo> s_resources = TServer.GetResourceInfos();
```

Add to the method switch:

```csharp
"resources/list" => HandleResourcesList(request),
"resources/read" => await HandleResourcesReadAsync(request, ct),
```

Implement handlers:

```csharp
private static JsonRpcResponse HandleResourcesList(JsonRpcRequest request)
{
    return SuccessResponse(request.Id, BuildResourcesListResult());
}

private static JsonElement BuildResourcesListResult()
{
    using var ms = new MemoryStream();
    using (var w = new Utf8JsonWriter(ms))
    {
        w.WriteStartObject();
        w.WriteStartArray("resources");
        foreach (var r in s_resources)
        {
            w.WriteStartObject();
            w.WriteString("uri", r.Uri);
            if (r.MimeType is not null) w.WriteString("mimeType", r.MimeType);
            if (r.Description is not null) w.WriteString("description", r.Description);
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }
    return JsonDocument.Parse(ms.ToArray()).RootElement.Clone();
}

private async Task<JsonRpcResponse> HandleResourcesReadAsync(JsonRpcRequest request, CancellationToken ct)
{
    if (request.Params is not { } p)
        return ErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Missing params");
    if (!p.TryGetProperty("uri", out var uriEl) || uriEl.ValueKind != JsonValueKind.String)
        return ErrorResponse(request.Id, McpErrorCodes.InvalidParams, "Missing params.uri");

    var uri = uriEl.GetString()!;
    try
    {
        var result = await server.DispatchResourceReadAsync(uri, ct);
        return SuccessResponse(request.Id, BuildResourceReadResult(result));
    }
    catch (ArgumentException ex)
    {
        return ErrorResponse(request.Id, McpErrorCodes.InvalidParams, ex.Message);
    }
}

private static JsonElement BuildResourceReadResult(ResourceReadResult result)
{
    using var ms = new MemoryStream();
    using (var w = new Utf8JsonWriter(ms))
    {
        w.WriteStartObject();
        w.WriteStartArray("contents");
        w.WriteStartObject();
        w.WriteString(result.IsBinary ? "blob" : "text", result.Content);
        w.WriteEndObject();
        w.WriteEndArray();
        w.WriteEndObject();
    }
    return JsonDocument.Parse(ms.ToArray()).RootElement.Clone();
}
```

- [ ] **Step 3: Add `resources` capability to `BuildInitializeResult`**

After the `tools` capability block:

```csharp
w.WriteStartObject("resources");
w.WriteBoolean("listChanged", false);
w.WriteEndObject();
```

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```
feat(runtime): add resources/list and resources/read handlers to McpProtocolHandler
```

### Task 11: Write resource tests

**Files:**
- Modify: `tests/Qyl.Agents.Generator.Tests/McpServerGeneratorTests.cs`

- [ ] **Step 1: Write generator test for resource code generation**

```csharp
[Fact]
public async Task ResourceMethodGeneratesDispatchAndMetadata()
{
    var source = """
                 using Qyl.Agents;
                 using System.Threading;
                 using System.Threading.Tasks;

                 namespace ResTest;

                 /// <summary>Test</summary>
                 [McpServer]
                 public partial class ResServer
                 {
                     /// <summary>Agent config</summary>
                     [Tool]
                     public string Ping() => "pong";

                     /// <summary>Agent configuration file</summary>
                     [Resource("config://agents.toml", MimeType = "application/toml")]
                     public Task<string> GetConfigAsync(CancellationToken ct)
                         => Task.FromResult("key = \"value\"");
                 }
                 """;

    using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
    result
        .File("ResTest.ResServer.McpServer.g.cs", content =>
        {
            Assert.Contains("DispatchResourceReadAsync", content);
            Assert.Contains("GetResourceInfos", content);
            Assert.Contains("config://agents.toml", content);
            Assert.Contains("application/toml", content);
        })
        .Compiles();
}
```

- [ ] **Step 2: Write QA0013 test**

```csharp
[Fact]
public async Task ResourceInvalidReturnTypeReportsQA0013()
{
    var source = """
                 using Qyl.Agents;

                 /// <summary>Test</summary>
                 [McpServer]
                 public partial class BadResServer
                 {
                     /// <summary>Bad</summary>
                     [Tool]
                     public string Ping() => "pong";

                     /// <summary>Bad</summary>
                     [Resource("data://x")]
                     public int GetData() => 42;
                 }
                 """;

    using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
    result.HasDiagnostic("QA0013", DiagnosticSeverity.Error);
}
```

- [ ] **Step 3: Write QA0014 test**

```csharp
[Fact]
public async Task DuplicateResourceUriReportsQA0014()
{
    var source = """
                 using Qyl.Agents;

                 /// <summary>Test</summary>
                 [McpServer]
                 public partial class DupeResServer
                 {
                     /// <summary>A</summary>
                     [Tool]
                     public string Ping() => "pong";

                     /// <summary>First</summary>
                     [Resource("config://x")]
                     public string GetA() => "a";

                     /// <summary>Second</summary>
                     [Resource("config://x")]
                     public string GetB() => "b";
                 }
                 """;

    using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
    result.HasDiagnostic("QA0014", DiagnosticSeverity.Error);
}
```

- [ ] **Step 4: Run all tests**

Run: `dotnet test tests/Qyl.Agents.Generator.Tests/Qyl.Agents.Generator.Tests.csproj`
Expected: All pass

- [ ] **Step 5: Commit**

```
test: add resource generation and diagnostic tests
```

---

## Chunk 3: Prompts (Section 3)

### Task 12: Add `[Prompt]` types to Abstractions

**Files:**
- Create: `src/Qyl.Agents.Abstractions/PromptAttribute.cs`
- Create: `src/Qyl.Agents.Abstractions/PromptRole.cs`
- Create: `src/Qyl.Agents.Abstractions/PromptMessage.cs`
- Create: `src/Qyl.Agents.Abstractions/PromptResult.cs`
- Create: `src/Qyl.Agents.Abstractions/McpPromptArgument.cs`
- Create: `src/Qyl.Agents.Abstractions/McpPromptInfo.cs`

- [ ] **Step 1: Create all 6 files**

`PromptAttribute.cs`:
```csharp
namespace Qyl.Agents;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class PromptAttribute(string name) : Attribute
{
    public string Name { get; } = name;
    public string? Description { get; set; }
}
```

`PromptRole.cs`:
```csharp
namespace Qyl.Agents;

public static class PromptRole
{
    public const string User = "user";
    public const string Assistant = "assistant";
    public const string System = "system";
}
```

`PromptMessage.cs`:
```csharp
namespace Qyl.Agents;

public sealed class PromptMessage
{
    public PromptMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }

    public string Role { get; }
    public string Content { get; }
}
```

`PromptResult.cs`:
```csharp
namespace Qyl.Agents;

using System.Collections.Generic;

public sealed class PromptResult
{
    public PromptResult(IReadOnlyList<PromptMessage> messages)
    {
        Messages = messages;
    }

    public IReadOnlyList<PromptMessage> Messages { get; }
}
```

`McpPromptArgument.cs`:
```csharp
namespace Qyl.Agents;

public sealed class McpPromptArgument
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool Required { get; init; }
}
```

`McpPromptInfo.cs`:
```csharp
namespace Qyl.Agents;

using System.Collections.Generic;

public sealed class McpPromptInfo
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<McpPromptArgument> Arguments { get; init; } = System.Array.Empty<McpPromptArgument>();
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/Qyl.Agents.Abstractions/Qyl.Agents.Abstractions.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```
feat(abstractions): add [Prompt] attribute, PromptResult, PromptMessage, and related types
```

### Task 13: Add `PromptModel`, `PromptExtractor`, wire into `ServerExtractor`

**Files:**
- Create: `src/Qyl.Agents.Generator/Models/PromptModel.cs`
- Create: `src/Qyl.Agents.Generator/Extraction/PromptExtractor.cs`
- Modify: `src/Qyl.Agents.Generator/DiagnosticDescriptors.cs`
- Modify: `src/Qyl.Agents.Generator/Models/ServerModel.cs`
- Modify: `src/Qyl.Agents.Generator/Extraction/ServerExtractor.cs`

- [ ] **Step 1: Create `PromptModel.cs`**

```csharp
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
```

- [ ] **Step 2: Add QA0015 and QA0016 diagnostics**

```csharp
public static readonly DiagnosticDescriptor PromptUnsupportedReturnType = new(
    "QA0015",
    "Prompt method has unsupported return type",
    "Method '{0}' decorated with [Prompt] has unsupported return type '{1}' — supported types are string, PromptResult, Task<string>, Task<PromptResult>",
    Category,
    DiagnosticSeverity.Error,
    true);

public static readonly DiagnosticDescriptor DuplicatePromptName = new(
    "QA0016",
    "Duplicate prompt name",
    "Prompt name '{0}' is used by multiple methods in class '{1}'",
    Category,
    DiagnosticSeverity.Error,
    true);
```

- [ ] **Step 3: Create `PromptExtractor.cs`**

Same shape as `ResourceExtractor` but classifies return type as `string` (IsStructured=false) or `PromptResult` (IsStructured=true). Reuses `ParameterExtractor.ExtractParameters()` for prompt arguments.

- [ ] **Step 4: Add Prompts to `ServerModel`**

```csharp
internal readonly record struct ServerModel(
    string Namespace,
    string ClassName,
    string ServerName,
    string Description,
    string? Version,
    EquatableArray<TypeDeclarationModel> DeclarationChain,
    EquatableArray<ToolModel> Tools,
    EquatableArray<ResourceModel> Resources,
    EquatableArray<PromptModel> Prompts);
```

- [ ] **Step 5: Wire `ExtractPrompts` into `ServerExtractor`**

Same pattern as `ExtractResources` — add `PromptAttributeName` const, `ExtractPrompts` method, zip into final `ServerModel`.

- [ ] **Step 6: Build**

Run: `dotnet build src/Qyl.Agents.Generator/Qyl.Agents.Generator.csproj`
Expected: Build succeeded

- [ ] **Step 7: Commit**

```
feat(extraction): add PromptExtractor with QA0015/QA0016 diagnostics
```

### Task 14: Add `PromptDispatchEmitter`, update protocol handler

**Files:**
- Create: `src/Qyl.Agents.Generator/Generation/PromptDispatchEmitter.cs`
- Modify: `src/Qyl.Agents.Generator/Generation/OutputGenerator.cs`
- Modify: `src/Qyl.Agents.Generator/Generation/SkillEmitter.cs`
- Modify: `src/Qyl.Agents/IMcpServer.cs`
- Modify: `src/Qyl.Agents/Protocol/McpProtocolHandler.cs`

- [ ] **Step 1: Create `PromptDispatchEmitter.cs`**

Emits `DispatchPromptGetAsync` (switch on name, deserialize args, invoke user method, serialize to JSON messages) and `GetPromptInfos` (flat argument descriptors from `ToolParameterModel`).

For `IsStructured = false` (string return), the generated code wraps in a single user message:
```csharp
sb.AppendLine("var text = " + awaitPrefix + method + "(" + callArgs + ");");
sb.AppendLine("return \"{\\\"messages\\\":[{\\\"role\\\":\\\"user\\\",\\\"content\\\":{\\\"type\\\":\\\"text\\\",\\\"text\\\":\\\"\" + ");
// Use Utf8JsonWriter for correctness — raw string concat would break on special chars
```

Use `Utf8JsonWriter` pattern (same as `BuildToolCallResult`) for safety.

- [ ] **Step 2: Wire into `OutputGenerator`**

After `ResourceDispatchEmitter.Emit(sb, server);`:
```csharp
PromptDispatchEmitter.Emit(sb, server);
```

- [ ] **Step 3: Add `## Prompts` section to `SkillEmitter`**

After resources section:
```csharp
if (!server.Prompts.IsEmpty)
{
    md.AppendLine("## Prompts");
    md.AppendLine();
    foreach (var p in server.Prompts)
    {
        md.Append("### ").AppendLine(p.PromptName);
        md.AppendLine();
        md.AppendLine(p.Description);
        md.AppendLine();
        if (!p.Parameters.IsEmpty)
        {
            md.AppendLine("**Arguments:**");
            md.AppendLine();
            foreach (var arg in p.Parameters)
            {
                md.Append("- `").Append(arg.CamelCaseName).Append('`');
                if (arg.IsRequired) md.Append(" (required)");
                if (arg.Description is not null) md.Append(": ").Append(arg.Description);
                md.AppendLine();
            }
            md.AppendLine();
        }
    }
}
```

- [ ] **Step 4: Add prompt members to `IMcpServer`**

```csharp
static abstract IReadOnlyList<McpPromptInfo> GetPromptInfos();
Task<string> DispatchPromptGetAsync(string name, JsonElement arguments, CancellationToken cancellationToken = default);
```

- [ ] **Step 5: Add prompt handlers to `McpProtocolHandler`**

Add `prompts/list` and `prompts/get` cases to the switch. Add `prompts` capability to `BuildInitializeResult`.

- [ ] **Step 6: Build**

Run: `dotnet build`
Expected: Build succeeded

- [ ] **Step 7: Commit**

```
feat: add [Prompt] support — extraction, dispatch, protocol handlers, SKILL.md section
```

### Task 15: Write prompt tests

**Files:**
- Modify: `tests/Qyl.Agents.Generator.Tests/McpServerGeneratorTests.cs`

- [ ] **Step 1: Test string return prompt**

```csharp
[Fact]
public async Task PromptStringReturnGeneratesSingleMessage()
{
    var source = """
                 using Qyl.Agents;
                 using System.ComponentModel;

                 namespace PromptTest;

                 /// <summary>Test</summary>
                 [McpServer]
                 public partial class PromptServer
                 {
                     /// <summary>A tool</summary>
                     [Tool]
                     public string Ping() => "pong";

                     /// <summary>Analyze an error</summary>
                     [Prompt("diagnose")]
                     public string Diagnose([Description("Error ID")] string errorId)
                         => $"Analyze error {errorId}";
                 }
                 """;

    using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
    result
        .File("PromptTest.PromptServer.McpServer.g.cs", content =>
        {
            Assert.Contains("DispatchPromptGetAsync", content);
            Assert.Contains("GetPromptInfos", content);
            Assert.Contains("diagnose", content);
        })
        .Compiles();
}
```

- [ ] **Step 2: Test QA0015 and QA0016**

Same pattern as resource diagnostic tests.

- [ ] **Step 3: Run all tests**

Run: `dotnet test tests/Qyl.Agents.Generator.Tests/Qyl.Agents.Generator.Tests.csproj`
Expected: All pass

- [ ] **Step 4: Commit**

```
test: add prompt generation and diagnostic tests
```

---

## Chunk 4: LlmsEmitter (Section 4)

### Task 16: Add `LlmsEmitter` and update `IMcpServer`

**Files:**
- Create: `src/Qyl.Agents.Generator/Generation/LlmsEmitter.cs`
- Modify: `src/Qyl.Agents.Generator/Generation/OutputGenerator.cs`
- Modify: `src/Qyl.Agents/IMcpServer.cs`
- Modify: `src/Qyl.Agents/Hosting/McpHost.cs`

- [ ] **Step 1: Create `LlmsEmitter.cs`**

```csharp
namespace Qyl.Agents.Generator.Generation;

using System.Text;
using Models;

internal static class LlmsEmitter
{
    public static void Emit(IndentedStringBuilder sb, ServerModel server)
    {
        var content = BuildLlmsTxtContent(server);
        var escaped = content.Replace("\"", "\"\"");

        sb.AppendLine("private const string s_llmsTxt = @\"" + escaped + "\";");
        sb.AppendLine();
        sb.AppendLine("/// <summary>Returns the llms.txt content for LLM discovery.</summary>");
        sb.AppendLine("public static string LlmsTxt => s_llmsTxt;");
    }

    private static string BuildLlmsTxtContent(ServerModel server)
    {
        var txt = new StringBuilder();

        txt.Append("# ").AppendLine(server.ServerName);
        txt.AppendLine();
        txt.Append("> ").AppendLine(server.Description);
        txt.AppendLine();

        if (!server.Tools.IsEmpty)
        {
            txt.AppendLine("## Tools");
            txt.AppendLine();
            foreach (var tool in server.Tools)
            {
                // /mcp is the JSON-RPC endpoint — an LLM crawler hitting it via GET
                // won't get useful output. This is a spec limitation, not an implementation bug.
                txt.Append("- [").Append(tool.ToolName).Append("](/mcp): ").Append(tool.Description);
                var hints = BuildAnnotationHints(tool);
                if (hints.Length > 0)
                    txt.Append(" (").Append(hints).Append(')');
                txt.AppendLine();
            }
            txt.AppendLine();
        }

        if (!server.Resources.IsEmpty)
        {
            txt.AppendLine("## Resources");
            txt.AppendLine();
            foreach (var r in server.Resources)
            {
                txt.Append("- [").Append(r.Uri).Append("](/mcp): ").Append(r.Description ?? "");
                if (r.MimeType is not null)
                    txt.Append(" (").Append(r.MimeType).Append(')');
                txt.AppendLine();
            }
            txt.AppendLine();
        }

        if (!server.Prompts.IsEmpty)
        {
            txt.AppendLine("## Prompts");
            txt.AppendLine();
            foreach (var p in server.Prompts)
            {
                txt.Append("- [").Append(p.PromptName).Append("](/mcp): ").Append(p.Description);
                if (!p.Parameters.IsEmpty)
                {
                    var args = string.Join(", ", p.Parameters.Select(static a => a.CamelCaseName));
                    txt.Append(" (arguments: ").Append(args).Append(')');
                }
                txt.AppendLine();
            }
            txt.AppendLine();
        }

        return txt.ToString().TrimEnd();
    }

    private static string BuildAnnotationHints(ToolModel tool)
    {
        var hints = new List<string>();
        if (tool.ReadOnly == ToolHintValue.True) hints.Add("read-only");
        if (tool.Destructive == ToolHintValue.True) hints.Add("destructive");
        if (tool.Idempotent == ToolHintValue.True) hints.Add("idempotent");
        if (tool.OpenWorld == ToolHintValue.True) hints.Add("open-world");
        return string.Join(", ", hints);
    }
}
```

- [ ] **Step 2: Wire into `OutputGenerator`**

After `SkillEmitter.Emit(sb, server);`:
```csharp
sb.AppendLine();
LlmsEmitter.Emit(sb, server);
```

- [ ] **Step 3: Add `LlmsTxt` to `IMcpServer`**

```csharp
/// <summary>Returns llms.txt content for LLM discovery.</summary>
static abstract string LlmsTxt { get; }
```

- [ ] **Step 4: Add `WriteLlmsTxtAsync` to `McpHost`**

```csharp
public static async Task WriteLlmsTxtAsync<TServer>(
    string outputPath,
    CancellationToken ct = default) where TServer : class, IMcpServer
{
    var content = TServer.LlmsTxt;
    await File.WriteAllTextAsync(outputPath, content, ct);
}
```

- [ ] **Step 5: Build and run tests**

Run: `dotnet build && dotnet test tests/Qyl.Agents.Generator.Tests/Qyl.Agents.Generator.Tests.csproj`
Expected: Build succeeded, existing tests pass (they now emit LlmsTxt too)

- [ ] **Step 6: Write LlmsEmitter test**

```csharp
[Fact]
public async Task LlmsTxtGeneratedWithToolsSection()
{
    var source = """
                 using Qyl.Agents;

                 namespace LlmsTest;

                 /// <summary>A calc server</summary>
                 [McpServer]
                 public partial class CalcServer
                 {
                     /// <summary>Add numbers</summary>
                     [Tool(ReadOnly = ToolHint.True)]
                     public int Add(int a, int b) => a + b;
                 }
                 """;

    using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
    result
        .File("LlmsTest.CalcServer.McpServer.g.cs", content =>
        {
            Assert.Contains("LlmsTxt", content);
            Assert.Contains("s_llmsTxt", content);
            Assert.Contains("# calc-server", content);
            Assert.Contains("## Tools", content);
            Assert.Contains("[add](/mcp)", content);
            Assert.Contains("read-only", content);
        })
        .Compiles();
}
```

- [ ] **Step 7: Run tests**

Run: `dotnet test tests/Qyl.Agents.Generator.Tests/Qyl.Agents.Generator.Tests.csproj`
Expected: All pass

- [ ] **Step 8: Commit**

```
feat: add LlmsEmitter for llms.txt generation and WriteLlmsTxtAsync on McpHost
```

---

## Chunk 5: HTTP Transport (Section 5)

### Task 17: Create `Qyl.Agents.Http` package

**Files:**
- Create: `src/Qyl.Agents.Http/Qyl.Agents.Http.csproj`
- Create: `src/Qyl.Agents.Http/McpHttpHostExtensions.cs`
- Modify: `src/Qyl.Agents/Qyl.Agents.csproj`
- Modify: `netagents.slnx`

- [ ] **Step 1: Add InternalsVisibleTo to `Qyl.Agents.csproj`**

In the existing `<ItemGroup>` with `<InternalsVisibleTo>`:

```xml
<InternalsVisibleTo Include="Qyl.Agents.Http"/>
```

- [ ] **Step 2: Create `Qyl.Agents.Http.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>Qyl.Agents.Http</RootNamespace>

    <Description>HTTP transport for Qyl.Agents MCP servers: MapMcpServer extension and well-known discovery paths.</Description>
    <PackageTags>qyl;agents;mcp;http;kestrel;hosting</PackageTags>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App"/>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Qyl.Agents\Qyl.Agents.csproj"/>
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create `McpHttpHostExtensions.cs`**

```csharp
namespace Qyl.Agents.Http;

using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Qyl.Agents.Protocol;

public static class McpHttpHostExtensions
{
    public static WebApplication MapMcpServer<TServer>(this WebApplication app)
        where TServer : class, IMcpServer, new()
        => app.MapMcpServer(new TServer());

    public static WebApplication MapMcpServer<TServer>(this WebApplication app, TServer server)
        where TServer : class, IMcpServer
    {
        var handler = new McpProtocolHandler<TServer>(server);

        app.MapPost("/mcp", async (HttpContext ctx, CancellationToken ct) =>
        {
            JsonRpcRequest? request;
            try
            {
                request = await JsonSerializer.DeserializeAsync(
                    ctx.Request.Body, JsonRpcJsonContext.Default.JsonRpcRequest, ct);
            }
            catch (JsonException)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsJsonAsync(
                    new { jsonrpc = "2.0", error = new { code = -32700, message = "Invalid JSON" } }, ct);
                return;
            }

            if (request is null)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsJsonAsync(
                    new { jsonrpc = "2.0", error = new { code = -32600, message = "Null request" } }, ct);
                return;
            }

            var response = await handler.HandleAsync(request, ct);
            if (response is not null)
            {
                ctx.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(
                    ctx.Response.Body, response, JsonRpcJsonContext.Default.JsonRpcResponse, ct);
            }
        });

        // Well-known discovery paths
        app.MapGet("/skill.md", () => Results.Text(TServer.SkillMd, "text/markdown"));
        app.MapGet("/.well-known/skills/default/skill.md",
            () => Results.Text(TServer.SkillMd, "text/markdown"));
        app.MapGet("/llms.txt", () => Results.Text(TServer.LlmsTxt, "text/plain"));

        return app;
    }
}
```

- [ ] **Step 4: Add to solution**

In `netagents.slnx`, under `/src/`:

```xml
<Project Path="src/Qyl.Agents.Http/Qyl.Agents.Http.csproj"/>
```

- [ ] **Step 5: Build**

Run: `dotnet build`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```
feat: add Qyl.Agents.Http package with MapMcpServer<T>() and well-known discovery paths
```

### Task 18: Write HTTP integration tests

**Files:**
- Determine test project location — either add to `tests/Qyl.Agents.Tests/` or create `tests/Qyl.Agents.Http.Tests/`

- [ ] **Step 1: Create a minimal integration test**

Use `WebApplication.CreateBuilder()` in-process:

```csharp
[Fact]
public async Task McpInitializeOverHttp()
{
    var builder = WebApplication.CreateBuilder();
    var app = builder.Build();
    app.MapMcpServer<TestMcpServer>();

    await using var server = app;
    await server.StartAsync();

    using var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
    // ... POST /mcp with initialize request, assert response
}
```

Note: Exact test setup depends on test infrastructure preferences. `WebApplicationFactory` from `Microsoft.AspNetCore.Mvc.Testing` is cleaner but adds a dependency. In-process startup with port binding works without extra packages.

- [ ] **Step 2: Test GET /skill.md**

```csharp
var response = await client.GetAsync("/skill.md");
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
Assert.Equal("text/markdown", response.Content.Headers.ContentType?.MediaType);
```

- [ ] **Step 3: Test GET /llms.txt**

```csharp
var response = await client.GetAsync("/llms.txt");
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
```

- [ ] **Step 4: Run all tests**

Run: `dotnet test`
Expected: All pass

- [ ] **Step 5: Commit**

```
test: add HTTP transport integration tests for MCP and discovery endpoints
```

---

## Final Verification

### Task 19: Full build and test pass

- [ ] **Step 1: Clean build**

Run: `dotnet build --no-incremental`
Expected: 0 errors, only expected warnings (QA0009, QA0012 on test sources)

- [ ] **Step 2: Full test run**

Run: `dotnet test`
Expected: All tests pass across all test projects

- [ ] **Step 3: Verify existing tests still pass**

The existing 18 tests in `McpServerGeneratorTests.cs` must all pass unchanged. New QA0012 warnings are expected on tests without safety annotations but should not break compilation assertions.

- [ ] **Step 4: Final commit with any fixups**

```
chore: final verification pass — all tests green
```
