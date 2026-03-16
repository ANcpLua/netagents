namespace Qyl.Agents;

using System.Collections.Generic;

/// <summary>A structured prompt result containing multiple messages.</summary>
public sealed class PromptResult
{
    public PromptResult(IReadOnlyList<PromptMessage> messages)
    {
        Messages = messages;
    }

    /// <summary>Optional description for the prompt result.</summary>
    public string? Description { get; init; }

    /// <summary>The conversation messages in this prompt template.</summary>
    public IReadOnlyList<PromptMessage> Messages { get; }
}
