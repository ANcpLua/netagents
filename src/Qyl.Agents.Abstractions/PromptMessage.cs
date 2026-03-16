namespace Qyl.Agents;

/// <summary>A single message in an MCP prompt template.</summary>
public sealed class PromptMessage
{
    public PromptMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }

    /// <summary>Message role (user, assistant, system).</summary>
    public string Role { get; }

    /// <summary>Message content text.</summary>
    public string Content { get; }
}
