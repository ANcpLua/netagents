namespace Qyl.Agents;

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
