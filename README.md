[![NuGet](https://img.shields.io/nuget/v/NetAgents?label=NetAgents&color=0891B2)](https://www.nuget.org/packages/NetAgents/)
[![NuGet](https://img.shields.io/nuget/v/Qyl.Agents.Abstractions?label=Abstractions&color=7C3AED)](https://www.nuget.org/packages/Qyl.Agents.Abstractions/)
[![NuGet](https://img.shields.io/nuget/v/Qyl.Agents.Generator?label=Generator&color=D97706)](https://www.nuget.org/packages/Qyl.Agents.Generator/)
[![NuGet](https://img.shields.io/nuget/v/Qyl.Agents?label=Runtime&color=059669)](https://www.nuget.org/packages/Qyl.Agents/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

# netagents

`.agents` package manager and compile-time MCP server source generator for .NET.

## Packages

| Package | Purpose |
|---------|---------|
| `NetAgents` | CLI tool for bootstrapping, installing, syncing, and trusting `.agents` skill repositories |
| `Qyl.Agents.Abstractions` | `[McpServer]` and `[Tool]` marker attributes (`netstandard2.0`) |
| `Qyl.Agents.Generator` | Source generator that emits MCP dispatch, schema, metadata, and OTel instrumentation |
| `Qyl.Agents` | Runtime: MCP transport, protocol handler, HTTP hosting, well-known discovery |

## Installation

```bash
# CLI tool
dotnet tool install --global NetAgents

# MCP server libraries
dotnet add package Qyl.Agents.Abstractions
dotnet add package Qyl.Agents.Generator
dotnet add package Qyl.Agents
```

### Test tooling

This repo now uses `xunit.analyzers` (via central package versions) for xUnit analyzer rules and code fixes, including solution-wide `xUnit1051` cancellation-token fixes.

## Quick Start

```csharp
using Qyl.Agents;

[McpServer("calc-server")]
public partial class CalcServer
{
    [Tool]
    public int Add(int a, int b) => a + b;
}
```

The generator produces MCP dispatch and metadata at build time. The runtime package hosts the server over stdio or HTTP.

```bash
netagents init
netagents add getsentry/dotagents
netagents install
```

## Related

- [ANcpLua.Roslyn.Utilities](https://github.com/ANcpLua/ANcpLua.Roslyn.Utilities)
