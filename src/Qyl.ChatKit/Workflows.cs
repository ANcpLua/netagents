using System.Text.Json.Serialization;

namespace Qyl.ChatKit;

/// <summary>Workflow attached to a thread with optional summary.</summary>
public sealed record Workflow
{
    /// <summary>Workflow kind: "custom" or "reasoning".</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("tasks")]
    public IReadOnlyList<ChatKitTask> Tasks { get; init; } = [];

    [JsonPropertyName("summary")]
    public WorkflowSummary? Summary { get; init; }

    [JsonPropertyName("expanded")]
    public bool Expanded { get; init; }
}

/// <summary>Summary variants available for workflows.</summary>
[JsonPolymorphic]
[JsonDerivedType(typeof(CustomSummary), typeDiscriminator: "custom")]
[JsonDerivedType(typeof(DurationSummary), typeDiscriminator: "duration")]
public abstract record WorkflowSummary;

/// <summary>Custom summary for a workflow.</summary>
public sealed record CustomSummary : WorkflowSummary
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>Icon name. Accepts <c>vendor:*</c> and <c>lucide:*</c> prefixes.</summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; init; }
}

/// <summary>Summary providing total workflow duration.</summary>
public sealed record DurationSummary : WorkflowSummary
{
    /// <summary>The duration of the workflow in seconds.</summary>
    [JsonPropertyName("duration")]
    public required int Duration { get; init; }
}

// -- Task types (named ChatKitTask to avoid conflict with System.Threading.Tasks.Task) --

/// <summary>Base fields common to all workflow tasks.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CustomTask), "custom")]
[JsonDerivedType(typeof(SearchTask), "web_search")]
[JsonDerivedType(typeof(ThoughtTask), "thought")]
[JsonDerivedType(typeof(FileTask), "file")]
[JsonDerivedType(typeof(ImageTask), "image")]
public abstract record ChatKitTask
{
    /// <summary>
    /// Only used when rendering the task as part of a workflow.
    /// Indicates the status of the task.
    /// </summary>
    [JsonPropertyName("status_indicator")]
    public string StatusIndicator { get; init; } = "none";
}

/// <summary>Workflow task displaying custom content.</summary>
public sealed record CustomTask : ChatKitTask
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Icon name. Accepts <c>vendor:*</c> and <c>lucide:*</c> prefixes.</summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }
}

/// <summary>Workflow task representing a web search.</summary>
public sealed record SearchTask : ChatKitTask
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("title_query")]
    public string? TitleQuery { get; init; }

    [JsonPropertyName("queries")]
    public IReadOnlyList<string> Queries { get; init; } = [];

    [JsonPropertyName("sources")]
    public IReadOnlyList<UrlSource> Sources { get; init; } = [];
}

/// <summary>Workflow task capturing assistant reasoning.</summary>
public sealed record ThoughtTask : ChatKitTask
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }
}

/// <summary>Workflow task referencing file sources.</summary>
public sealed record FileTask : ChatKitTask
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("sources")]
    public IReadOnlyList<FileSource> Sources { get; init; } = [];
}

/// <summary>Workflow task rendering image content.</summary>
public sealed record ImageTask : ChatKitTask
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }
}
