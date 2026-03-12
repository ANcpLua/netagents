# netagents

Package manager for `.agents` directories + compile-time MCP server source generator.

## Build

```bash
dotnet build
dotnet test --project tests/NetAgents.Tests/NetAgents.Tests.csproj
dotnet test --project tests/Qyl.Agents.Generator.Tests/Qyl.Agents.Generator.Tests.csproj
```

## Architecture

```
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
├── Qyl.Agents.Abstractions/      # [McpServer], [Tool] marker attributes (netstandard2.0)
├── Qyl.Agents.Generator/         # IIncrementalGenerator: Extraction → Models → Generation
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

## Cross-Repo Dependencies

- `Qyl.Agents.Generator` → `ANcpLua.Roslyn.Utilities` (EquatableArray, DiagnosticFlow, etc.)
- `Qyl.Agents.Generator.Tests` → `ANcpLua.Roslyn.Utilities.Testing` (GeneratorTestEngine)
- `Qyl.Agents.Tests` → `ANcpLua.Roslyn.Utilities.Testing.AgentTesting`

These reference `../ANcpLua.Roslyn.Utilities/` as a sibling repo on disk.

## Conventions

- C# 14, .NET 10, file-scoped namespaces, primary constructors
- `[GeneratedRegex]` for all regex patterns
- `TimeProvider.System` instead of `DateTime.Now`
- `Lock` instead of `object` for locks
- `System.Text.Json` only (no Newtonsoft)
- Early returns, pattern matching, switch expressions
- Tomlyn for TOML, Spectre.Console for terminal UI
- Roslyn generators: IIncrementalGenerator, ForAttributeWithMetadataName, value-equatable models
- xUnit v3 for tests, `dotnet test --project <path>` (MTP v2 runner)
