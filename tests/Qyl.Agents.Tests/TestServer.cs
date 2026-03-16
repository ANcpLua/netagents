namespace Qyl.Agents.Tests;

using System.ComponentModel;

/// <summary>A calculator MCP server for end-to-end testing.</summary>
[McpServer]
public partial class CalcServer
{
    public int CallCount { get; private set; }

    /// <summary>Adds two numbers</summary>
    [Tool(ReadOnly = ToolHint.True, Idempotent = ToolHint.True)]
    public int Add([Description("First number")] int a, [Description("Second number")] int b)
    {
        CallCount++;
        return a + b;
    }

    /// <summary>Multiplies two numbers</summary>
    [Tool(ReadOnly = ToolHint.True, Idempotent = ToolHint.True)]
    public Task<int> Multiply([Description("First factor")] int a, [Description("Second factor")] int b,
        CancellationToken ct)
    {
        CallCount++;
        return Task.FromResult(a * b);
    }

    /// <summary>Always throws for error testing</summary>
    [Tool(Destructive = ToolHint.False)]
    public string Fail([Description("Error message")] string message)
    {
        CallCount++;
        throw new InvalidOperationException(message);
    }
}
