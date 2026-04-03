using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Qyl.ChatKit;

/// <summary>
/// Converts ChatKit <see cref="ThreadItem"/> instances to MAF <see cref="ChatMessage"/>
/// objects for sending to an <see cref="IChatClient"/>.
/// </summary>
public class ThreadItemConverter
{
    /// <summary>Convert an attachment into a content part for the model.</summary>
    public virtual ValueTask<AIContent> AttachmentToContentAsync(AttachmentBase attachment) =>
        throw new NotImplementedException(
            "An Attachment was included in a UserMessageItem but AttachmentToContentAsync was not implemented.");

    /// <summary>Convert a tag into a content part for the model.</summary>
    public virtual ValueTask<AIContent> TagToContentAsync(UserMessageTagContent tag) =>
        throw new NotImplementedException(
            "A Tag was included in a UserMessageItem but TagToContentAsync was not implemented.");

    public virtual ValueTask<ChatMessage?> ConvertUserMessageAsync(
        UserMessageItem item, bool isLastMessage = true)
    {
        List<string> textParts = [];
        List<UserMessageTagContent> rawTags = [];

        foreach (var part in item.Content)
        {
            switch (part)
            {
                case UserMessageTextContent text:
                    textParts.Add(text.Text);
                    break;
                case UserMessageTagContent tag:
                    textParts.Add($"@{tag.Text}");
                    rawTags.Add(tag);
                    break;
            }
        }

        var userMessage = new ChatMessage(ChatRole.User, string.Concat(textParts));

        // Quoted text context
        if (item.QuotedText is not null && isLastMessage)
        {
            userMessage.Contents.Add(new TextContent(
                $"\nThe user is referring to this in particular: \n{item.QuotedText}"));
        }

        // Deduplicated tag context
        if (rawTags.Count > 0)
        {
            HashSet<string> seen = [];
            List<UserMessageTagContent> uniqueTags = [];
            foreach (var tag in rawTags)
            {
                if (seen.Add(tag.Text))
                    uniqueTags.Add(tag);
            }

            if (uniqueTags.Count > 0)
            {
                userMessage.Contents.Add(new TextContent(
                    """
                    # User-provided context for @-mentions
                    - When referencing resolved entities, use their canonical names **without** '@'.
                    - The '@' form appears only in user text and should not be echoed.
                    """));
            }
        }

        return new ValueTask<ChatMessage?>(userMessage);
    }

    public virtual ValueTask<ChatMessage?> ConvertAssistantMessageAsync(AssistantMessageItem item)
    {
        var contents = item.Content
            .Select(c => (AIContent)new TextContent(c.Text))
            .ToList();

        return new ValueTask<ChatMessage?>(new ChatMessage(ChatRole.Assistant, contents));
    }

    public virtual ValueTask<ChatMessage?> ConvertClientToolCallAsync(ClientToolCallItem item)
    {
        if (item.Status == "pending")
            return new ValueTask<ChatMessage?>((ChatMessage?)null);

        var args = JsonSerializer.Serialize(item.Arguments);
        var output = JsonSerializer.Serialize(item.Output);

        var message = new ChatMessage(ChatRole.Assistant,
        [
            new FunctionCallContent(item.CallId, item.Name, new Dictionary<string, object?> { ["args"] = args }),
        ]);

        // The result message follows
        var resultMessage = new ChatMessage(ChatRole.Tool,
        [
            new FunctionResultContent(item.CallId, output),
        ]);

        // Return the function call; the caller collects both via ToAgentInputAsync
        return new ValueTask<ChatMessage?>(message);
    }

    public virtual ValueTask<ChatMessage?> ConvertWidgetAsync(WidgetItem item)
    {
        var json = JsonSerializer.Serialize(item.Widget, ChatKitJsonOptions.Default);
        return new ValueTask<ChatMessage?>(new ChatMessage(ChatRole.User,
            $"The following graphical UI widget (id: {item.Id}) was displayed to the user:{json}"));
    }

    public virtual ValueTask<ChatMessage?> ConvertWorkflowAsync(WorkflowItem item)
    {
        var messages = new List<string>();
        foreach (var task in item.Workflow.Tasks)
        {
            if (task is not CustomTask custom || (custom.Title is null && custom.Content is null))
                continue;

            var title = custom.Title ?? "";
            var content = custom.Content ?? "";
            var taskText = title.Length > 0 && content.Length > 0
                ? $"{title}: {content}"
                : title.Length > 0 ? title : content;

            messages.Add(
                $"A message was displayed to the user that the following task was performed:\n<Task>\n{taskText}\n</Task>");
        }

        if (messages.Count == 0)
            return new ValueTask<ChatMessage?>((ChatMessage?)null);

        return new ValueTask<ChatMessage?>(
            new ChatMessage(ChatRole.User, string.Join("\n", messages)));
    }

    public virtual ValueTask<ChatMessage?> ConvertTaskAsync(TaskItem item)
    {
        if (item.Task is not CustomTask custom || (custom.Title is null && custom.Content is null))
            return new ValueTask<ChatMessage?>((ChatMessage?)null);

        var title = custom.Title ?? "";
        var content = custom.Content ?? "";
        var taskText = title.Length > 0 && content.Length > 0
            ? $"{title}: {content}"
            : title.Length > 0 ? title : content;

        return new ValueTask<ChatMessage?>(new ChatMessage(ChatRole.User,
            $"A message was displayed to the user that the following task was performed:\n<Task>\n{taskText}\n</Task>"));
    }

    public virtual ValueTask<ChatMessage?> ConvertHiddenContextAsync(HiddenContextItem item)
    {
        if (item.Content is not string text)
            throw new NotImplementedException(
                "HiddenContextItems with non-string content require a custom ConvertHiddenContextAsync override.");

        return new ValueTask<ChatMessage?>(new ChatMessage(ChatRole.User,
            $"Hidden context for the agent (not shown to the user):\n<HiddenContext>\n{text}\n</HiddenContext>"));
    }

    public virtual ValueTask<ChatMessage?> ConvertSdkHiddenContextAsync(SdkHiddenContextItem item) =>
        new(new ChatMessage(ChatRole.User,
            $"Hidden context for the agent (not shown to the user):\n<HiddenContext>\n{item.Content}\n</HiddenContext>"));

    public virtual ValueTask<ChatMessage?> ConvertGeneratedImageAsync(GeneratedImageItem item)
    {
        if (item.Image is null)
            return new ValueTask<ChatMessage?>((ChatMessage?)null);

        return new ValueTask<ChatMessage?>(new ChatMessage(ChatRole.User,
        [
            new TextContent("The following image was generated by the agent."),
            new UriContent(new Uri(item.Image.Url), "image/png"),
        ]));
    }

    public virtual ValueTask<ChatMessage?> ConvertEndOfTurnAsync(EndOfTurnItem item) =>
        new((ChatMessage?)null);

    /// <summary>
    /// Convert a sequence of thread items into MAF chat messages for model input.
    /// </summary>
    public async ValueTask<IList<ChatMessage>> ToAgentInputAsync(
        IReadOnlyList<ThreadItem> items)
    {
        var output = new List<ChatMessage>();

        for (var i = 0; i < items.Count; i++)
        {
            var isLast = i == items.Count - 1;
            var message = items[i] switch
            {
                UserMessageItem user => await ConvertUserMessageAsync(user, isLast),
                AssistantMessageItem assistant => await ConvertAssistantMessageAsync(assistant),
                ClientToolCallItem toolCall => await ConvertClientToolCallAsync(toolCall),
                WidgetItem widget => await ConvertWidgetAsync(widget),
                WorkflowItem workflow => await ConvertWorkflowAsync(workflow),
                TaskItem task => await ConvertTaskAsync(task),
                HiddenContextItem hidden => await ConvertHiddenContextAsync(hidden),
                SdkHiddenContextItem sdkHidden => await ConvertSdkHiddenContextAsync(sdkHidden),
                GeneratedImageItem image => await ConvertGeneratedImageAsync(image),
                EndOfTurnItem eot => await ConvertEndOfTurnAsync(eot),
                _ => null,
            };

            if (message is not null)
                output.Add(message);
        }

        return output;
    }
}
