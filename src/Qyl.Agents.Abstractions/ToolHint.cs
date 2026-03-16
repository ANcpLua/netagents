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
