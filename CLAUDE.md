# netagents

C# transpilation of dotagents — a package manager for `.agents` directories.

## Build

```bash
dotnet build
dotnet test
```

## Architecture

```
src/NetAgents/
├── Program.cs              # CLI entry point, command routing
├── Scope.cs                # Project/user scope resolution
├── Cli/Commands/           # init, install, add, remove, sync, list, mcp, trust, doctor
├── Agents/                 # Agent definitions, MCP/hook config writers
├── Config/                 # agents.toml schema, loader, writer
├── Lockfile/               # agents.lock schema, loader, writer
├── Skills/                 # SKILL.md loader, discovery, resolver
├── Sources/                # git.ts, local.ts, cache.ts
├── Symlinks/               # Symlink creation/management
├── Trust/                  # Trust policy validation
├── Gitignore/              # .agents/.gitignore generation
└── Utils/                  # ProcessRunner, FileSystem helpers
```

## Conventions

- C# 14, .NET 10, file-scoped namespaces, primary constructors
- `[GeneratedRegex]` for all regex patterns
- `TimeProvider.System` instead of `DateTime.Now`
- `Lock` instead of `object` for locks
- `System.Text.Json` only (no Newtonsoft)
- Early returns, pattern matching, switch expressions
- Tomlyn for TOML, Spectre.Console for terminal UI
- xUnit v3 for tests, co-located in tests/NetAgents.Tests/
