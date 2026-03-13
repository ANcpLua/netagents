# AGENTS.md

**Scope:** This repository root and all child directories unless a deeper `AGENTS.md` overrides it.

## Role of this repository

This is the source of truth for `netagents`, the `.agents` package manager and compile-time MCP server source generator.

## Hard rules

- Prefer the existing CLI, generator, runtime, and symlink patterns before inventing new abstractions.
- Keep cross-repo integration with `ANcpLua.Roslyn.Utilities` explicit and local.
- Preserve public behavior unless there is a clear correctness reason to change it.
- Use `System.Text.Json`, `TimeProvider.System`, `[GeneratedRegex]`, and `Lock` consistently with existing code.

## Preferred toolchain

- **Source search/edit/builds:** `mcp__rider__*` tools when available.
- **UI verification:** `mcp__playwright__*` tools.
- **Build/test commands:** use the repo's existing `dotnet` commands unless the user asks for something narrower.

## Runtime/compiler context

- C# 14 and .NET 10 are the baseline.
- `Qyl.Agents.Abstractions` stays `netstandard2.0`.

## Build and verification

- `dotnet build`
- `dotnet test --project tests/NetAgents.Tests/NetAgents.Tests.csproj`
- `dotnet test --project tests/Qyl.Agents.Generator.Tests/Qyl.Agents.Generator.Tests.csproj`

## Architecture map

```text
src/
├── NetAgents/                     # CLI tool (dotnet tool)
│   ├── Program.cs                 # CLI entry point, command routing
│   ├── Scope.cs                   # Project/user scope resolution
│   ├── Cli/Commands/              # init, install, add, remove, sync, list, mcp, trust, doctor
│   ├── Mcp/                       # NetAgentsMcpServer — MCP server via Qyl.Agents
│   ├── Agents/                    # Agent definitions, MCP/hook config writers
│   ├── Config/                    # agents.toml schema, loader, writer
│   ├── Lockfile/                  # agents.lock schema, loader, writer
│   ├── Skills/                    # SKILL.md loader, discovery, resolver
│   ├── Sources/                   # GitSource, LocalSource, SkillCache
│   ├── Symlinks/                  # Symlink creation/management
│   ├── Trust/                     # Trust policy validation
│   ├── Gitignore/                 # .agents/.gitignore generation
│   └── Utils/                     # ProcessRunner, FileSystem helpers
├── Qyl.Agents.Abstractions/       # [McpServer], [Tool] marker attributes (netstandard2.0)
├── Qyl.Agents.Generator/          # IIncrementalGenerator: Extraction -> Models -> Generation
│   ├── Extraction/                # ServerExtractor, ToolExtractor, ParameterExtractor
│   ├── Models/                    # ServerModel, ToolModel, ToolParameterModel (value-equatable)
│   └── Generation/                # DispatchEmitter, SchemaEmitter, OTelEmitter, JsonContextEmitter, MetadataEmitter
└── Qyl.Agents/                    # Runtime: McpHost (stdio), McpProtocolHandler (JSON-RPC)

tests/
├── NetAgents.Tests/
├── Qyl.Agents.Generator.Tests/
└── Qyl.Agents.Tests/

shared/Polyfills/                  # netstandard2.0 polyfills for Abstractions + Generator
```

## Cross-repo dependencies

- `Qyl.Agents.Generator` -> `ANcpLua.Roslyn.Utilities`
- `Qyl.Agents.Generator.Tests` -> `ANcpLua.Roslyn.Utilities.Testing`
- `Qyl.Agents.Tests` -> `ANcpLua.Roslyn.Utilities.Testing.AgentTesting`

These resolve to `../ANcpLua.Roslyn.Utilities/` as a sibling repo on disk.

## Code style defaults

- File-scoped namespaces and primary constructors.
- Early returns, pattern matching, and switch expressions.
- Tomlyn for TOML and Spectre.Console for terminal UI.
- Roslyn generators use `IIncrementalGenerator`, `ForAttributeWithMetadataName`, and value-equatable models.
- xUnit v3 with `dotnet test --project <path>`.
