# netagents

`netagents` publishes a small set of packages for managing `.agents` skill repositories and building compile-time MCP servers in .NET.

## Packages

- `NetAgents`: a .NET tool for initializing, installing, syncing, and trusting `.agents` directories.
- `Qyl.Agents.Abstractions`: `[McpServer]` and `[Tool]` attributes used by the generator.
- `Qyl.Agents.Generator`: incremental source generator that emits MCP dispatch, metadata, schema, and telemetry glue.
- `Qyl.Agents`: runtime protocol and hosting helpers for generated servers.

## Install

```bash
dotnet tool install --global NetAgents
dotnet add package Qyl.Agents.Abstractions
dotnet add package Qyl.Agents.Generator
dotnet add package Qyl.Agents
```

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

The generator produces the MCP-facing dispatch and metadata at build time. The runtime package provides the protocol host and handler used to serve generated MCP servers.

For repository management, initialize a project with:

```bash
netagents init
netagents add getsentry/dotagents
netagents install
```

## Repository

- Source: https://github.com/ANcpLua/netagents
- License: MIT
