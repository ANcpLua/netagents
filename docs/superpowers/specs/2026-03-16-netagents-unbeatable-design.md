# netagents: Compile-Time MCP Server Generator Enhancements

**Date:** 2026-03-16
**Status:** Approved
**Scope:** Generator + Abstractions + Runtime (no CLI additions)

## Problem

netagents is the only compile-time MCP server source generator for .NET. The generator moat is real — nobody else does this. But the generated servers currently only speak stdio, only expose tools (no resources or prompts), lack safety annotations required by the Anthropic MCP Directory, and produce only SKILL.md for discovery. This limits adoption to CLI-piped agents and blocks directory submission.

## Goal

Make netagents-generated MCP servers first-class citizens in the broader AI agent ecosystem by closing every gap between "compiles" and "discoverable, submittable, and deployable without boilerplate."

## Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Scope | Generator + Abstractions + Runtime | CLI distribution (`netagents publish/serve`) deferred — no registry exists yet |
| HTTP packaging | Separate `Qyl.Agents.Http` package | Stdio consumers shouldn't pay for Kestrel. Clean package boundary |
| Safety annotations | Explicit tri-state enum + QA warning | Convention inference is fragile. `bool?` invalid on attributes — use `ToolHint` enum (`Unset`/`True`/`False`). Absent = unknown, explicit false = developer said no |
| `[Resource]` target | Methods only | CancellationToken requires methods. Properties create false sync promise |
| `[Resource]` returns | `string` + `byte[]` via `ResourceReadResult` | Binary resources from day one. IsBinary flag preserves text/blob distinction at runtime |
| `[Resource]` URIs | Static strings only | URI templates (RFC 6570) deferred — adds parser complexity for v1 |
| `[Prompt]` return detection | `string` (single message) or `PromptResult` (structured) | Same return-type classification pattern as `[Tool]`. Zero cognitive load |
| `[Prompt]` arguments | Flat `McpPromptArgument` descriptors | MCP spec uses name/description/required, not JSON Schema |
| `PromptRole` | String constants, not enum | MCP role field is open string. Enum forces breaking change on spec additions |
| HTTP API shape | `MapMcpServer<T>` extension on `WebApplication` | Composable with existing ASP.NET hosts. Two overloads: `new()` + instance for DI |
| SSE transport | Deferred | POST covers discovery + stateless tool calls. SSE adds bidirectional complexity |

## Implementation Order

Matches section numbering in this document:

1. Safety annotations on `[Tool]` (Anthropic directory unblock)
2. `[Resource]` attribute + extraction + generation
3. `[Prompt]` attribute + extraction + generation
4. `LlmsEmitter` (parallel to SkillEmitter, requires Sections 1-3 for full model)
5. `Qyl.Agents.Http` package (ships last, depends on Section 4 `LlmsTxt`)

## Section 1: Safety Annotations on `[Tool]`

### Abstractions

C# attribute properties cannot be `bool?` (CS0655). Use a tri-state enum instead:

```csharp
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

Add four `ToolHint` properties to `ToolAttribute`:

```csharp
public ToolHint ReadOnly { get; set; }
public ToolHint Destructive { get; set; }
public ToolHint Idempotent { get; set; }
public ToolHint OpenWorld { get; set; }
```

Default is `ToolHint.Unset` (zero-initialized). Developer usage:

```csharp
[Tool(ReadOnly = ToolHint.True, Idempotent = ToolHint.True)]
public async Task<string> ListAsync(string projectRoot, CancellationToken ct)
```

All netstandard2.0 compatible (enums are valid attribute parameters). No new dependencies.

### Model

Expand `ToolModel` with four `ToolHint` fields:

```csharp
internal readonly record struct ToolModel(
    string MethodName,
    string ToolName,
    string Description,
    string ResultTypeFullyQualified,
    ReturnKind ReturnKind,
    bool HasCancellationToken,
    EquatableArray<ToolParameterModel> Parameters,
    ToolHint ReadOnly,       // NEW — maps to ToolHint enum
    ToolHint Destructive,    // NEW
    ToolHint Idempotent,     // NEW
    ToolHint OpenWorld);     // NEW
```

Note: `ToolHint` is a generator-internal enum mirroring the Abstractions `ToolHint` by value (byte 0/1/2). The generator reads the attribute's integer value and maps it.

### Extraction

`ToolExtractor.Extract()` reads four annotation values via `GetNamedArgument<int>()` (enums are stored as their underlying type in attribute data) and maps to `ToolHint`.

### Generation — Annotations Flow

Annotations are **not** part of `inputSchema` — they are a sibling field in the `tools/list` response. The data flows through:

1. **`McpToolInfo`** (Abstractions) — grows four `ToolHint` fields matching the attribute
2. **`MetadataEmitter`** — emits annotation values into `GetToolInfos()` return alongside Name, Description, InputSchema
3. **`McpProtocolHandler.BuildToolsListResult()`** — writes `"annotations":{...}` object per tool, with per-field guards

`SchemaEmitter` stays untouched — it only produces `inputSchema` byte arrays.

**Per-field emission rules:**
- Object-level guard: emit `"annotations":{...}` only when at least one field is not `Unset`
- Field-level guard: emit each hint field only when its `ToolHint` is `True` or `False`
- `Unset` = omit field entirely ("developer didn't say" — unknown to agent)
- `True` = emit `true` ("developer said yes")
- `False` = emit `false` ("developer said no")

Wire format when only `Idempotent = ToolHint.True` is set:

```json
{
  "name": "install-package",
  "inputSchema": {...},
  "annotations": {
    "idempotentHint": true
  }
}
```

### Generation — SkillEmitter

Append annotation hints in SKILL.md tool sections when any are non-Unset.

### Diagnostics

New `QA0012` — "Tool has no safety annotations" (Warning). Fires when all four `ToolHint` values are `Unset`. Same pattern as `QA0009` (parameter missing description).

### Files Changed

| File | Change |
|------|--------|
| `src/Qyl.Agents.Abstractions/ToolHint.cs` | NEW — tri-state enum (Unset/True/False) |
| `src/Qyl.Agents.Abstractions/ToolAttribute.cs` | Add 4 `ToolHint` properties |
| `src/Qyl.Agents.Generator/Models/ToolModel.cs` | Add 4 `ToolHint` fields |
| `src/Qyl.Agents.Abstractions/McpToolInfo.cs` | Add 4 `ToolHint` fields for annotation transport |
| `src/Qyl.Agents.Generator/Extraction/ToolExtractor.cs` | Read annotations + emit QA0012 |
| `src/Qyl.Agents.Generator/Generation/MetadataEmitter.cs` | Emit ToolHint values into GetToolInfos() |
| `src/Qyl.Agents/Protocol/McpProtocolHandler.cs` | Write annotations object in BuildToolsListResult() |
| `src/Qyl.Agents.Generator/Generation/SkillEmitter.cs` | Emit annotation hints in markdown |
| `src/Qyl.Agents.Generator/DiagnosticDescriptors.cs` | Add QA0012 |
| `tests/Qyl.Agents.Generator.Tests/McpServerGeneratorTests.cs` | Test annotation emission + QA0012 |

## Section 2: `[Resource]` Attribute + Extraction + Generation

### New Types in Abstractions

```csharp
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ResourceAttribute(string uri) : Attribute
{
    public string Uri { get; } = uri;
    public string? MimeType { get; set; }
    public string? Description { get; set; }
}

public sealed class McpResourceInfo
{
    public required string Uri { get; init; }
    public string? MimeType { get; init; }
    public string? Description { get; init; }
}

public sealed class ResourceReadResult(string content, bool isBinary)
{
    public string Content { get; } = content;
    public bool IsBinary { get; } = isBinary;
}
```

### Model

```csharp
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

### Extraction — `ResourceExtractor.cs`

Same shape as `ToolExtractor`:
- Validates: not static, not generic (reuse `SemanticGuard`)
- Return type must unwrap to `string` or `byte[]`
- `IsBinary = true` when return unwraps to `byte[]`
- Reads `Uri`, `MimeType`, `Description` from attribute
- `QA0013`: unsupported return type (Error)
- `QA0014`: duplicate URI within same server (Error)

### ServerModel Expansion

```csharp
internal readonly record struct ServerModel(
    ...,
    EquatableArray<ToolModel> Tools,
    EquatableArray<ResourceModel> Resources);
```

### Generation — `ResourceDispatchEmitter.cs`

Emits:
- `DispatchResourceReadAsync(string uri, CancellationToken ct)` — switch on URI, dispatch to user method
  - `byte[]` methods: `return new ResourceReadResult(Convert.ToBase64String(raw), true)`
  - `string` methods: `return new ResourceReadResult(raw, false)`
- `static IReadOnlyList<McpResourceInfo> GetResourceInfos()`

### IMcpServer Expansion

```csharp
static abstract IReadOnlyList<McpResourceInfo> GetResourceInfos();
Task<ResourceReadResult> DispatchResourceReadAsync(string uri, CancellationToken ct);
```

### McpProtocolHandler Expansion

Two new cases in method switch:
- `"resources/list"` — builds JSON from `GetResourceInfos()`
- `"resources/read"` — calls `server.DispatchResourceReadAsync(uri, ct)`, emits `"text"` or `"blob"` key based on `IsBinary`

Capability advertisement in `BuildInitializeResult`:
```json
"capabilities": {
  "tools": { "listChanged": false },
  "resources": { "listChanged": false }
}
```

### SkillEmitter Expansion

New `## Resources` section after `## Tools`.

### Diagnostics

| Code | Message | Severity |
|------|---------|----------|
| QA0013 | Resource method has unsupported return type | Error |
| QA0014 | Duplicate resource URI | Error |

### Files Changed

| File | Change |
|------|--------|
| `src/Qyl.Agents.Abstractions/ResourceAttribute.cs` | NEW |
| `src/Qyl.Agents.Abstractions/McpResourceInfo.cs` | NEW |
| `src/Qyl.Agents.Abstractions/ResourceReadResult.cs` | NEW |
| `src/Qyl.Agents.Generator/Models/ResourceModel.cs` | NEW |
| `src/Qyl.Agents.Generator/Extraction/ResourceExtractor.cs` | NEW |
| `src/Qyl.Agents.Generator/Extraction/ServerExtractor.cs` | Extract resources alongside tools |
| `src/Qyl.Agents.Generator/Generation/ResourceDispatchEmitter.cs` | NEW |
| `src/Qyl.Agents.Generator/Generation/OutputGenerator.cs` | Add ResourceDispatchEmitter call |
| `src/Qyl.Agents.Generator/Generation/MetadataEmitter.cs` | Emit GetResourceInfos() |
| `src/Qyl.Agents.Generator/Generation/SkillEmitter.cs` | Add ## Resources section |
| `src/Qyl.Agents.Generator/Models/ServerModel.cs` | Add Resources field |
| `src/Qyl.Agents.Generator/DiagnosticDescriptors.cs` | Add QA0013, QA0014 |
| `src/Qyl.Agents/IMcpServer.cs` | Add resource members |
| `src/Qyl.Agents/Protocol/McpProtocolHandler.cs` | Add resources/list, resources/read |

Note: `McpResourceInfo` lives only in `Qyl.Agents.Abstractions`, following the established pattern for `McpToolInfo` and `McpServerInfo`. The `Qyl.Agents` project references Abstractions and uses the type directly.

## Section 3: `[Prompt]` Attribute + Extraction + Generation

### New Types in Abstractions

```csharp
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class PromptAttribute(string name) : Attribute
{
    public string Name { get; } = name;
    public string? Description { get; set; }
}

public static class PromptRole
{
    public const string User = "user";
    public const string Assistant = "assistant";
    public const string System = "system";
}

public sealed class PromptMessage(string role, string content)
{
    public string Role { get; } = role;
    public string Content { get; } = content;
}

public sealed class PromptResult(IReadOnlyList<PromptMessage> messages)
{
    public IReadOnlyList<PromptMessage> Messages { get; } = messages;
}

public sealed class McpPromptArgument
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool Required { get; init; }
}

public sealed class McpPromptInfo
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<McpPromptArgument> Arguments { get; init; } = [];
}
```

`PromptRole` uses string constants, not enum — MCP role field is open string on wire.

### Model

```csharp
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

`IsStructured`: `true` when return unwraps to `PromptResult`, `false` for `string`.

### Extraction — `PromptExtractor.cs`

Same shape as Tool/Resource extractors:
- Not static, not generic
- Return type unwraps to `string` or `PromptResult`
- Parameters extracted via `ParameterExtractor.ExtractParameters()` (reused)
- `QA0015`: unsupported return type (Error)
- `QA0016`: duplicate prompt name (Error)

### ServerModel Expansion

```csharp
internal readonly record struct ServerModel(
    ...,
    EquatableArray<ResourceModel> Resources,
    EquatableArray<PromptModel> Prompts);
```

### Generation — `PromptDispatchEmitter.cs`

Emits:
- `DispatchPromptGetAsync(string name, JsonElement arguments, CancellationToken ct)` — switch on name, deserialize args (same pattern as DispatchEmitter), invoke user method, serialize result as pre-serialized JSON messages array
- `static IReadOnlyList<McpPromptInfo> GetPromptInfos()` — builds flat `McpPromptArgument` list from `ToolParameterModel.CamelCaseName`/`.Description`/`.IsRequired`

Wire format for string return (`IsStructured = false`):
```json
{ "messages": [{ "role": "user", "content": { "type": "text", "text": "..." } }] }
```

Wire format for PromptResult return (`IsStructured = true`):
```json
{ "messages": [
    { "role": "user", "content": { "type": "text", "text": "..." } },
    { "role": "assistant", "content": { "type": "text", "text": "..." } }
] }
```

`SchemaEmitter` stays untouched — prompts use flat argument descriptors, not JSON Schema.

### IMcpServer Expansion

```csharp
static abstract IReadOnlyList<McpPromptInfo> GetPromptInfos();
Task<string> DispatchPromptGetAsync(string name, JsonElement arguments, CancellationToken ct);
```

### McpProtocolHandler Expansion

Two new cases:
- `"prompts/list"` — builds JSON from `GetPromptInfos()`
- `"prompts/get"` — calls `server.DispatchPromptGetAsync(name, arguments, ct)`, embeds pre-serialized JSON

Capability advertisement:
```json
"capabilities": {
  "tools": { "listChanged": false },
  "resources": { "listChanged": false },
  "prompts": { "listChanged": false }
}
```

### SkillEmitter Expansion

New `## Prompts` section with argument listing.

### Diagnostics

| Code | Message | Severity |
|------|---------|----------|
| QA0015 | Prompt method has unsupported return type | Error |
| QA0016 | Duplicate prompt name | Error |

### Files Changed

| File | Change |
|------|--------|
| `src/Qyl.Agents.Abstractions/PromptAttribute.cs` | NEW |
| `src/Qyl.Agents.Abstractions/PromptRole.cs` | NEW |
| `src/Qyl.Agents.Abstractions/PromptMessage.cs` | NEW |
| `src/Qyl.Agents.Abstractions/PromptResult.cs` | NEW |
| `src/Qyl.Agents.Abstractions/McpPromptArgument.cs` | NEW |
| `src/Qyl.Agents.Abstractions/McpPromptInfo.cs` | NEW |
| `src/Qyl.Agents.Generator/Models/PromptModel.cs` | NEW |
| `src/Qyl.Agents.Generator/Extraction/PromptExtractor.cs` | NEW |
| `src/Qyl.Agents.Generator/Extraction/ServerExtractor.cs` | Extract prompts alongside tools/resources |
| `src/Qyl.Agents.Generator/Generation/PromptDispatchEmitter.cs` | NEW |
| `src/Qyl.Agents.Generator/Generation/OutputGenerator.cs` | Add PromptDispatchEmitter call |
| `src/Qyl.Agents.Generator/Generation/MetadataEmitter.cs` | Emit GetPromptInfos() |
| `src/Qyl.Agents.Generator/Generation/SkillEmitter.cs` | Add ## Prompts section |
| `src/Qyl.Agents.Generator/Models/ServerModel.cs` | Add Prompts field |
| `src/Qyl.Agents.Generator/DiagnosticDescriptors.cs` | Add QA0015, QA0016 |
| `src/Qyl.Agents/IMcpServer.cs` | Add prompt members |
| `src/Qyl.Agents/Protocol/McpProtocolHandler.cs` | Add prompts/list, prompts/get |

## Section 4: `LlmsEmitter` — llms.txt Generation

### New Emitter — `LlmsEmitter.cs`

Same shape as `SkillEmitter`. Consumes full `ServerModel`, produces plaintext.

Output format:
```
# {server-name}

> {description}

## Tools

- [{tool-name}](/mcp): {description} ({annotation hints if non-Unset})

## Resources

- [{uri}](/mcp): {description} ({mimeType})

## Prompts

- [{prompt-name}](/mcp): {description} (arguments: {arg1, arg2})
```

Sections omitted when empty. The `/mcp` link target is the single JSON-RPC endpoint — an LLM crawler hitting it via GET won't get useful output, but this is a spec limitation, not an implementation bug. One-line comment in `LlmsEmitter.cs` to prevent "fixes."

### IMcpServer Expansion

```csharp
static abstract string LlmsTxt { get; }
```

### McpHost Expansion

```csharp
public static async Task WriteLlmsTxtAsync<TServer>(
    string outputPath, CancellationToken ct = default)
    where TServer : class, IMcpServer
{
    await File.WriteAllTextAsync(outputPath, TServer.LlmsTxt, ct);
}
```

### OutputGenerator Update

Emission order:
```
OTelEmitter -> SchemaEmitter -> DispatchEmitter -> ResourceDispatchEmitter
-> PromptDispatchEmitter -> MetadataEmitter -> SkillEmitter -> LlmsEmitter
```

### No New Diagnostics

LlmsEmitter is a pure projection of already-validated `ServerModel`.

### Files Changed

| File | Change |
|------|--------|
| `src/Qyl.Agents.Generator/Generation/LlmsEmitter.cs` | NEW |
| `src/Qyl.Agents.Generator/Generation/OutputGenerator.cs` | Add LlmsEmitter call |
| `src/Qyl.Agents/IMcpServer.cs` | Add LlmsTxt property |
| `src/Qyl.Agents/Hosting/McpHost.cs` | Add WriteLlmsTxtAsync |

## Section 5: `Qyl.Agents.Http` — HTTP Transport + Well-Known Paths

**Depends on:** Sections 1-4 (requires `LlmsTxt` on `IMcpServer` from Section 4, safety annotations from Section 1).

### New Package

| Property | Value |
|----------|-------|
| Package | `Qyl.Agents.Http` |
| TargetFramework | `net10.0` |
| Dependencies | `Qyl.Agents`, `Microsoft.AspNetCore.App` (framework ref) |

### Public API

Two overloads — instance overload carries logic, `new()` overload delegates:

```csharp
// Standalone use
public static WebApplication MapMcpServer<TServer>(this WebApplication app)
    where TServer : class, IMcpServer, new()
    => app.MapMcpServer(new TServer());

// DI / factory use
public static WebApplication MapMcpServer<TServer>(this WebApplication app, TServer server)
    where TServer : class, IMcpServer
{
    var handler = new McpProtocolHandler<TServer>(server);

    // MCP JSON-RPC endpoint
    app.MapPost("/mcp", async (HttpContext ctx, CancellationToken ct) => { /* ... */ });

    // Well-known discovery paths
    app.MapGet("/skill.md", () => Results.Text(TServer.SkillMd, "text/markdown"));
    app.MapGet("/.well-known/skills/default/skill.md",
        () => Results.Text(TServer.SkillMd, "text/markdown"));
    app.MapGet("/llms.txt", () => Results.Text(TServer.LlmsTxt, "text/plain"));

    return app;
}
```

### Visibility

`<InternalsVisibleTo Include="Qyl.Agents.Http"/>` in `Qyl.Agents.csproj` (alongside existing `Qyl.Agents.Tests` entry) — grants access to `McpProtocolHandler`, `JsonRpcRequest`, `JsonRpcResponse`, `JsonRpcJsonContext` without making them public.

### Well-Known Paths

| Path | Content | Content-Type |
|------|---------|-------------|
| `POST /mcp` | JSON-RPC MCP protocol | `application/json` |
| `GET /skill.md` | Generated SKILL.md | `text/markdown` |
| `GET /.well-known/skills/default/skill.md` | Same SKILL.md | `text/markdown` |
| `GET /llms.txt` | Generated llms.txt | `text/plain` |

### Deferred

- SSE transport (bidirectional streaming complexity)
- OAuth/authentication middleware
- CORS configuration (consumers add their own)

### OTel

No additional OTel code. Existing `McpProtocolHandler` spans are inherited. Kestrel adds HTTP-level spans via ASP.NET `DiagnosticSource`.

### Files Changed

| File | Change |
|------|--------|
| `src/Qyl.Agents.Http/Qyl.Agents.Http.csproj` | NEW |
| `src/Qyl.Agents.Http/McpHttpHostExtensions.cs` | NEW |
| `src/Qyl.Agents/Qyl.Agents.csproj` | Add `<InternalsVisibleTo Include="Qyl.Agents.Http"/>` (follows existing pattern for test project) |
| `netagents.slnx` | Add Qyl.Agents.Http project |

## Diagnostic Code Registry

| Code | Message | Severity | Section |
|------|---------|----------|---------|
| QA0001-QA0011 | Existing | — | — |
| QA0012 | Tool has no safety annotations | Warning | 1 (Safety) |
| QA0013 | Resource method has unsupported return type | Error | 2 (Resource) |
| QA0014 | Duplicate resource URI | Error | 2 (Resource) |
| QA0015 | Prompt method has unsupported return type | Error | 3 (Prompt) |
| QA0016 | Duplicate prompt name | Error | 3 (Prompt) |

## Cross-Cutting Concerns

### netstandard2.0 Compatibility

All new types in `Qyl.Agents.Abstractions` stay netstandard2.0. `PromptResult`, `PromptMessage`, `ResourceReadResult` use `IReadOnlyList<T>` and constructor-based classes (consistent with `McpToolInfo`/`McpServerInfo` pattern). `Qyl.Agents.Http` targets net10.0 — no constraint.

### AOT Safety

All dispatch is compile-time generated. No reflection. `ResourceReadResult` and `PromptResult` are known types — `JsonContextEmitter` adds `[JsonSerializable]` entries if needed.

### Breaking Change on IMcpServer

Adding `LlmsTxt`, `GetResourceInfos()`, `GetPromptInfos()`, `DispatchResourceReadAsync()`, `DispatchPromptGetAsync()` to `IMcpServer` is a breaking change for anyone manually implementing it. Acceptable because: (a) nobody implements `IMcpServer` manually — the generator does, and (b) the generator always emits all members. Existing [McpServer] classes that have no resources or prompts get empty implementations generated automatically.

### Backward Compatibility for Existing Servers

Servers with only `[Tool]` methods continue to work. The generator emits:
- `GetResourceInfos()` returning empty array
- `GetPromptInfos()` returning empty array
- `DispatchResourceReadAsync()` throwing `ArgumentException` for any URI
- `DispatchPromptGetAsync()` throwing `ArgumentException` for any name
- `LlmsTxt` with only `## Tools` section
- `BuildInitializeResult` always advertises all three capabilities (`tools`, `resources`, `prompts`) regardless of content. Advertising an empty capability is valid per MCP spec — a client sending `resources/list` gets back an empty array, which is correct. This keeps `BuildInitializeResult` as a static structure with no runtime branching, preserving the AOT-safe, zero-allocation property

## Test Infrastructure

### Existing Patterns

| Layer | Pattern |
|---|---|
| **Generator** | `Test<McpServerGenerator>.Run(source, ct)` → fluent `.Produces()` / `.File()` / `.HasDiagnostic()` / `.Compiles()` |
| **Protocol** | `McpProtocolHandler<TServer>` instantiated directly, real `JsonRpcRequest` objects |
| **OTel** | `ActivityCollector("Qyl.Agents")` + `collector.FindSingle(...)` + `.AssertTag()` |
| **References** | `TestConfiguration.WithAdditionalReferences(typeof(McpServerAttribute))` — for new attributes (`ResourceAttribute`, `PromptAttribute`, `ToolHint`), add their types the same way |
| **CancellationToken** | Always `TestContext.Current.CancellationToken` |
| **Test servers** | `CalcServer` lives directly in the test file as a real `[McpServer] public partial class` — new test servers (`ResourceServer`, `PromptServer`) defined alongside it |

### Section 1 Tests — Safety Annotations

**Generator test: hint serialization** — Source with `[Tool(ReadOnly = ToolHint.True, Idempotent = ToolHint.True)]`. Assert via `.File()`: `"idempotentHint": true` present, no `"readOnlyHint"` / `"destructiveHint"` emitted (Unset values omitted).

**Generator test: QA0012** — All four hints `Unset`. Assert: `.HasDiagnostic("QA0012", DiagnosticSeverity.Warning)`.

### Section 2 Tests — Resources

**Generator test: code generation** — `[Resource("config://x")]` on `Task<string>` method. Assert: `DispatchResourceReadAsync` + `GetResourceInfos` generated.

**Protocol test: list + read** — `McpProtocolHandler<ResourceServer>`. Send `resources/list` → assert metadata. Send `resources/read` with URI → assert content.

**Generator test: QA0013** — `[Resource("config://y")]` on method returning `int`. Assert error diagnostic.

**Generator test: QA0014** — Two methods with `[Resource("config://x")]`. Assert duplicate URI error.

### Section 3 Tests — Prompts

**Generator test: string return** — `[Prompt]` on method returning `string`. Assert single user-role message wrapping.

**Generator test: PromptResult return** — `[Prompt]` on method returning `PromptResult`. Assert structured message passthrough.

**Generator test: QA0015** — Invalid return type. Assert error diagnostic.

**Generator test: QA0016** — Duplicate prompt name. Assert error diagnostic.

### Section 4 Tests — LlmsEmitter

**Generator test: emitted content** — Assert `LlmsTxt` property + `s_llmsTxt` const generated. Content contains `# calc-server`, `## Tools`, `- [add](/mcp)`.

### Section 5 Tests — HTTP Transport

No generator tests. Integration-level only.

**Test 1: MCP Initialize** — `WebApplicationFactory` / in-process `WebApplication`. `app.MapMcpServer<CalcServer>()`. POST `/mcp` with initialize request. Assert capabilities.

**Test 2: Skill.md** — GET `/skill.md` → HTTP 200 + `text/markdown`.

**Test 3: llms.txt** — GET `/llms.txt` → HTTP 200 + `text/plain`.
