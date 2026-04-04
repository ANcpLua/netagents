namespace Qyl.Agents.Generator.Tests;

using ANcpLua.Roslyn.Utilities.Testing;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

public sealed class McpServerGeneratorTests : IDisposable
{
    private readonly IDisposable _refs = TestConfiguration.WithAdditionalReferences(typeof(McpServerAttribute));

    public void Dispose()
    {
        _refs.Dispose();
    }

    [Fact]
    public async Task SingleToolGeneratesFullOutput()
    {
        var source = """
                     using Qyl.Agents;
                     using System.Threading;
                     using System.Threading.Tasks;

                     namespace TestApp;

                     /// <summary>A test server</summary>
                     [McpServer]
                     public partial class MyTools
                     {
                         /// <summary>Echoes the input</summary>
                         [Tool]
                         public Task<string> Echo(string message, CancellationToken cancellationToken)
                         {
                             return Task.FromResult(message);
                         }
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .Produces("TestApp.MyTools.McpServer.g.cs")
            .File("TestApp.MyTools.McpServer.g.cs", content =>
            {
                content.Should().Contain("DispatchToolCallAsync");
                content.Should().Contain("ExecuteTool_EchoAsync");
                content.Should().Contain("GetServerInfo");
                content.Should().Contain("GetToolInfos");
                content.Should().Contain("s_schema_Echo");
                content.Should().NotContain("s_jsonOptions");
                content.Should().Contain("GetString()");
                content.Should().Contain("SkillMd");
                content.Should().Contain("s_skillMd");
                content.Should().Contain("gen_ai.operation.name");
                content.Should().Contain("gen_ai.tool.name");
                content.Should().Contain("ActivityKind.Internal");
                content.Should().Contain("await ");
                content.Should().Contain("cancellationToken");
                content.Should().Contain("gen_ai.system");
                content.Should().Contain("server.name");
                content.Should().Contain("gen_ai.client.operation.duration");
                content.Should().NotContain("qyl.agent.tool.calls");
            })
            .Compiles();
    }

    [Fact]
    public async Task NonPartialClassReportsQA0001()
    {
        var source = """
                     using Qyl.Agents;

                     [McpServer]
                     public class NotPartial
                     {
                         [Tool]
                         public string Greet(string name) => $"Hello {name}";
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0001", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task SkillMdContainsFrontmatterAndToolDocs()
    {
        var source = """
                     using Qyl.Agents;
                     using System.ComponentModel;

                     namespace Docs;

                     /// <summary>A doc server</summary>
                     [McpServer]
                     public partial class DocTools
                     {
                         /// <summary>Search documents</summary>
                         [Tool]
                         public string Search([Description("Query text")] string query, int limit) => query;
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .File("Docs.DocTools.McpServer.g.cs", content =>
            {
                content.Should().Contain("name: doc-tools");
                content.Should().Contain("description: A doc server");
                content.Should().Contain("---");
                content.Should().Contain("### search");
                content.Should().Contain("Search documents");
                content.Should().Contain("`query` (string, required): Query text");
                content.Should().Contain("`limit` (integer, required)");
            })
            .Compiles();
    }

    [Fact]
    public async Task MultipleToolsAllRouted()
    {
        var source = """
                     using Qyl.Agents;

                     namespace Multi;

                     /// <summary>Multi-tool</summary>
                     [McpServer]
                     public partial class ToolBox
                     {
                         /// <summary>Add</summary>
                         [Tool]
                         public int Add(int a, int b) => a + b;

                         /// <summary>Sub</summary>
                         [Tool]
                         public int Subtract(int a, int b) => a - b;
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .File("Multi.ToolBox.McpServer.g.cs", content =>
            {
                content.Should().Contain("ExecuteTool_AddAsync");
                content.Should().Contain("ExecuteTool_SubtractAsync");
            })
            .Compiles();
    }

    [Fact]
    public async Task StaticClassReportsQA0002()
    {
        var source = """
                     using Qyl.Agents;

                     [McpServer]
                     public static partial class StaticServer
                     {
                         [Tool]
                         public static string Greet(string name) => name;
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0002", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task GenericClassReportsQA0003()
    {
        var source = """
                     using Qyl.Agents;

                     [McpServer]
                     public partial class GenericServer<T>
                     {
                         [Tool]
                         public string Greet(string name) => name;
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0003", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task OrphanedToolReportsQA0004()
    {
        var source = """
                     using Qyl.Agents;

                     public partial class NotAServer
                     {
                         [Tool]
                         public string Orphan(string input) => input;
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0004", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task StaticMethodReportsQA0005()
    {
        var source = """
                     using Qyl.Agents;

                     /// <summary>Test</summary>
                     [McpServer]
                     public partial class StaticMethodServer
                     {
                         [Tool]
                         public static string Greet(string name) => name;
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0005", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task GenericMethodReportsQA0006()
    {
        var source = """
                     using Qyl.Agents;

                     /// <summary>Test</summary>
                     [McpServer]
                     public partial class GenericMethodServer
                     {
                         [Tool]
                         public T Greet<T>(T input) => input;
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0006", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task UnsupportedReturnTypeReportsQA0007()
    {
        var source = """
                     using Qyl.Agents;

                     /// <summary>Test</summary>
                     [McpServer]
                     public partial class BadReturnServer
                     {
                         [Tool]
                         public unsafe int* Stream(string input) => throw new System.NotImplementedException();
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0007", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task UnsupportedParameterTypeReportsQA0008()
    {
        var source = """
                     using Qyl.Agents;

                     /// <summary>Test</summary>
                     [McpServer]
                     public partial class BadParamServer
                     {
                         /// <summary>A tool</summary>
                         [Tool]
                         public string Process(System.Action callback) => "";
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0008", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task MissingDescriptionReportsQA0009ButCompiles()
    {
        var source = """
                     using Qyl.Agents;

                     /// <summary>Test</summary>
                     [McpServer]
                     public partial class NoDescServer
                     {
                         /// <summary>A tool</summary>
                         [Tool]
                         public string Echo(string input) => input;
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .HasDiagnostic("QA0009", DiagnosticSeverity.Warning)
            .Compiles();
    }

    [Fact]
    public async Task NoToolsReportsQA0010()
    {
        var source = """
                     using Qyl.Agents;

                     /// <summary>Empty server</summary>
                     [McpServer]
                     public partial class EmptyServer
                     {
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0010", DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task DuplicateToolNameReportsQA0011()
    {
        var source = """
                     using Qyl.Agents;

                     /// <summary>Test</summary>
                     [McpServer]
                     public partial class DuplicateServer
                     {
                         /// <summary>A</summary>
                         [Tool("dupe")]
                         public string First(string a) => a;

                         /// <summary>B</summary>
                         [Tool("dupe")]
                         public string Second(string b) => b;
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0011", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task NestedPartialClassCompiles()
    {
        var source = """
                     using Qyl.Agents;

                     namespace Outer;

                     public partial class Container
                     {
                         /// <summary>Nested server</summary>
                         [McpServer]
                         public partial class Inner
                         {
                             /// <summary>Hello</summary>
                             [Tool]
                             public string Greet(string name) => $"Hello {name}";
                         }
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .Produces("Outer.Inner.McpServer.g.cs")
            .Compiles();
    }

    [Fact]
    public async Task EnumParameterIncludedInSchemaAndJsonContext()
    {
        var source = """
                     using Qyl.Agents;
                     using System.ComponentModel;

                     namespace EnumTest;

                     public enum Priority { Low, Medium, High }

                     /// <summary>Test</summary>
                     [McpServer]
                     public partial class EnumServer
                     {
                         /// <summary>Set priority</summary>
                         [Tool]
                         public string SetPriority([Description("The priority")] Priority p) => p.ToString();
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .File("EnumTest.EnumServer.McpServer.g.cs", content =>
            {
                content.Should().Contain("Low");
                content.Should().Contain("Medium");
                content.Should().Contain("High");
                content.Should().NotContain("s_jsonOptions");
                content.Should().Contain("Enum.Parse");
                content.Should().Contain("Priority");
            })
            .Compiles();
    }

    [Fact]
    public async Task NullableParameterHandledInJsonContext()
    {
        var source = """
                     using Qyl.Agents;
                     using System.ComponentModel;

                     namespace NullableTest;

                     /// <summary>Test</summary>
                     [McpServer]
                     public partial class NullableServer
                     {
                         /// <summary>Process</summary>
                         [Tool]
                         public string Process([Description("Count")] int? count) => (count ?? 0).ToString();
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .File("NullableTest.NullableServer.McpServer.g.cs", content =>
            {
                content.Should().NotContain("s_jsonOptions");
                content.Should().Contain("GetInt32()");
                content.Should().Contain("JsonValueKind.Null");
            })
            .Compiles();
    }

    [Fact]
    public async Task JsonSerializationUsesConfiguredOptions()
    {
        var source = """
                     using Qyl.Agents;
                     using System.ComponentModel;

                     namespace JsonCtx;

                     /// <summary>Test</summary>
                     [McpServer]
                     public partial class CtxServer
                     {
                         /// <summary>Echo</summary>
                         [Tool]
                         public string Echo([Description("Input")] string input) => input;
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .File("JsonCtx.CtxServer.McpServer.g.cs", content =>
            {
                content.Should().NotContain("s_jsonOptions");
                content.Should().Contain("GetString()");
            })
            .Compiles();
    }

    [Fact]
    public async Task DateTimeOffsetGuidUriParametersHaveFormat()
    {
        var source = """
                     using Qyl.Agents;
                     using System;
                     using System.ComponentModel;

                     namespace FormatTest;

                     /// <summary>Test</summary>
                     [McpServer]
                     public partial class FormatServer
                     {
                         /// <summary>Process dates and ids</summary>
                         [Tool]
                         public string Process(
                             [Description("When")] DateTimeOffset when,
                             [Description("Id")] Guid id,
                             [Description("Link")] Uri link) => "";
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .File("FormatTest.FormatServer.McpServer.g.cs", content =>
            {
                content.Should().Contain("date-time");
                content.Should().Contain("uuid");
                content.Should().Contain("uri");
            })
            .Compiles();
    }

    [Fact]
    public async Task ArrayParameterIncludedInJsonContext()
    {
        var source = """
                     using Qyl.Agents;
                     using System.ComponentModel;

                     namespace ArrayTest;

                     /// <summary>Test</summary>
                     [McpServer]
                     public partial class ArrayServer
                     {
                         /// <summary>Process items</summary>
                         [Tool]
                         public string Process([Description("Items")] string[] items) => string.Join(",", items);
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .File("ArrayTest.ArrayServer.McpServer.g.cs", content =>
            {
                content.Should().Contain("s_jsonOptions");
                content.Should().Contain("array");
            })
            .Compiles();
    }

    [Fact]
    public async Task MultiLineDescriptionUsesYamlLiteralBlock()
    {
        var source = """
                     using Qyl.Agents;
                     using System.ComponentModel;

                     namespace MultiLine;

                     /// <summary>
                     /// A server that does things.
                     /// It has a multi-line description.
                     /// </summary>
                     [McpServer]
                     public partial class MultiServer
                     {
                         /// <summary>A tool</summary>
                         [Tool]
                         public string Echo([Description("Input")] string input) => input;
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .File("MultiLine.MultiServer.McpServer.g.cs", content =>
            {
                content.Should().Contain("description:");
            })
            .Compiles();
    }

    [Fact]
    public async Task ServerNameAttributeEmitted()
    {
        var source = """
                     using Qyl.Agents;
                     using System.Threading.Tasks;

                     namespace OTelTest;

                     /// <summary>Test</summary>
                     [McpServer]
                     public partial class OTelServer
                     {
                         /// <summary>Do thing</summary>
                         [Tool]
                         public Task<string> DoThing(string input) => Task.FromResult(input);
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .File("OTelTest.OTelServer.McpServer.g.cs", content =>
            {
                content.Should().Contain("SetTag(\"server.name\"");
                content.Should().Contain("SetTag(\"gen_ai.system\", \"mcp\")");
            })
            .Compiles();
    }

    [Fact]
    public async Task SafetyAnnotationsEmittedInGeneratedCode()
    {
        var source = """
                     using Qyl.Agents;
                     using System.ComponentModel;

                     namespace SafetyTest;

                     /// <summary>A calc server</summary>
                     [McpServer]
                     public partial class CalcServer
                     {
                         /// <summary>Adds two numbers</summary>
                         [Tool(ReadOnly = ToolHint.True, Idempotent = ToolHint.True)]
                         public int Add([Description("First")] int a, [Description("Second")] int b) => a + b;
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .File("SafetyTest.CalcServer.McpServer.g.cs", content =>
            {
                content.Should().Contain("ReadOnlyHint = true");
                content.Should().Contain("IdempotentHint = true");
                content.Should().NotContain("DestructiveHint");
                content.Should().NotContain("OpenWorldHint");
            })
            .Compiles();
    }

    [Fact]
    public async Task MissingSafetyAnnotationsReportsQA0012()
    {
        var source = """
                     using Qyl.Agents;

                     /// <summary>Test</summary>
                     [McpServer]
                     public partial class NoHintServer
                     {
                         /// <summary>A tool</summary>
                         [Tool]
                         public string Echo(string input) => input;
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .HasDiagnostic("QA0012", DiagnosticSeverity.Warning)
            .Compiles();
    }

    [Fact]
    public async Task ResourceMethodGeneratesDispatchAndMetadata()
    {
        var source = """
                     using Qyl.Agents;
                     using System.ComponentModel;
                     using System.Threading;
                     using System.Threading.Tasks;

                     namespace ResTest;

                     /// <summary>A resource server</summary>
                     [McpServer]
                     public partial class ResServer
                     {
                         /// <summary>Reads config</summary>
                         [Resource("config://agents.toml", MimeType = "application/toml")]
                         public Task<string> ReadConfig(CancellationToken ct) => Task.FromResult("key = true");

                         /// <summary>A tool</summary>
                         [Tool(ReadOnly = ToolHint.True)]
                         public string Ping([Description("Input")] string input) => input;
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .File("ResTest.ResServer.McpServer.g.cs", content =>
            {
                content.Should().Contain("DispatchResourceReadAsync");
                content.Should().Contain("GetResourceInfos");
                content.Should().Contain("config://agents.toml");
                content.Should().Contain("application/toml");
            })
            .Compiles();
    }

    [Fact]
    public async Task ResourceInvalidReturnTypeReportsQA0013()
    {
        var source = """
                     using Qyl.Agents;

                     /// <summary>Test</summary>
                     [McpServer]
                     public partial class BadResServer
                     {
                         /// <summary>Bad resource</summary>
                         [Resource("data://x")]
                         public int BadResource() => 42;

                         /// <summary>A tool</summary>
                         [Tool(ReadOnly = ToolHint.True)]
                         public string Ping(string input) => input;
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0013", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task DuplicateResourceUriReportsQA0014()
    {
        var source = """
                     using Qyl.Agents;

                     /// <summary>Test</summary>
                     [McpServer]
                     public partial class DupeResServer
                     {
                         /// <summary>First</summary>
                         [Resource("config://x")]
                         public string First() => "a";

                         /// <summary>Second</summary>
                         [Resource("config://x")]
                         public string Second() => "b";

                         /// <summary>A tool</summary>
                         [Tool(ReadOnly = ToolHint.True)]
                         public string Ping(string input) => input;
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0014", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task PromptStringReturnGeneratesSingleMessage()
    {
        var source = """
                     using Qyl.Agents;
                     using System.ComponentModel;

                     namespace PromptTest;

                     /// <summary>A prompt server</summary>
                     [McpServer]
                     public partial class PromptServer
                     {
                         /// <summary>Diagnose issues</summary>
                         [Prompt("diagnose")]
                         public string Diagnose([Description("Error text")] string error) => $"Diagnose: {error}";

                         /// <summary>A tool</summary>
                         [Tool(ReadOnly = ToolHint.True)]
                         public string Ping([Description("Input")] string input) => input;
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .File("PromptTest.PromptServer.McpServer.g.cs", content =>
            {
                content.Should().Contain("DispatchPromptAsync");
                content.Should().Contain("GetPromptInfos");
                content.Should().Contain("diagnose");
            })
            .Compiles();
    }

    [Fact]
    public async Task PromptInvalidReturnTypeReportsQA0015()
    {
        var source = """
                     using Qyl.Agents;

                     /// <summary>Test</summary>
                     [McpServer]
                     public partial class BadPromptServer
                     {
                         /// <summary>Bad prompt</summary>
                         [Prompt("bad")]
                         public int BadPrompt() => 42;

                         /// <summary>A tool</summary>
                         [Tool(ReadOnly = ToolHint.True)]
                         public string Ping(string input) => input;
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0015", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task DuplicatePromptNameReportsQA0016()
    {
        var source = """
                     using Qyl.Agents;

                     /// <summary>Test</summary>
                     [McpServer]
                     public partial class DupePromptServer
                     {
                         /// <summary>First</summary>
                         [Prompt("dupe")]
                         public string First(string a) => a;

                         /// <summary>Second</summary>
                         [Prompt("dupe")]
                         public string Second(string b) => b;

                         /// <summary>A tool</summary>
                         [Tool(ReadOnly = ToolHint.True)]
                         public string Ping(string input) => input;
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result.HasDiagnostic("QA0016", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task LlmsTxtGeneratedWithToolsSection()
    {
        var source = """
                     using Qyl.Agents;
                     using System.ComponentModel;

                     namespace LlmsTest;

                     /// <summary>A calc server</summary>
                     [McpServer]
                     public partial class CalcServer
                     {
                         /// <summary>Adds two numbers</summary>
                         [Tool(ReadOnly = ToolHint.True)]
                         public int Add([Description("First")] int a, [Description("Second")] int b) => a + b;
                     }
                     """;

        using var result = await Test<McpServerGenerator>.Run(source, TestContext.Current.CancellationToken);
        result
            .File("LlmsTest.CalcServer.McpServer.g.cs", content =>
            {
                content.Should().Contain("LlmsTxt");
                content.Should().Contain("s_llmsTxt");
                content.Should().Contain("# calc-server");
                content.Should().Contain("## Tools");
                content.Should().Contain("[add](/mcp)");
                content.Should().Contain("read-only");
            })
            .Compiles();
    }
}
