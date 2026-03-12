using System.Text.Json.Serialization;
using NetAgents.Cli.Commands;

namespace NetAgents;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(IReadOnlyList<SkillStatus>))]
[JsonSerializable(typeof(IReadOnlyList<McpCommand.McpListEntry>))]
[JsonSerializable(typeof(IReadOnlyList<TrustCommand.TrustListEntry>))]
internal partial class NetAgentsJsonContext : JsonSerializerContext;
