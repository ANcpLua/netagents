using System.Runtime.CompilerServices;
using System.Text.Json;
using Qyl.ChatKit.Widgets;

namespace Qyl.ChatKit;

/// <summary>
/// Compares two <see cref="WidgetRoot"/> instances and produces streaming deltas,
/// and streams widget roots as <see cref="ThreadStreamEvent"/> sequences.
/// </summary>
public static class WidgetDiff
{
    /// <summary>
    /// Compare two widget roots and return a list of deltas describing
    /// the minimal updates needed to transform <paramref name="before"/>
    /// into <paramref name="after"/>.
    /// </summary>
    public static IReadOnlyList<ThreadItemUpdate> DiffWidget(WidgetRoot before, WidgetRoot after)
    {
        if (FullReplace(before, after))
            return [new WidgetRootUpdated { Widget = after }];

        var beforeNodes = FindAllStreamingTextComponents(before);
        var afterNodes = FindAllStreamingTextComponents(after);

        List<ThreadItemUpdate> deltas = [];

        foreach (var (id, afterNode) in afterNodes)
        {
            if (!beforeNodes.TryGetValue(id, out var beforeNode))
            {
                throw new InvalidOperationException(
                    $"Node {id} was not present when the widget was initially rendered. " +
                    "All nodes with ID must persist across all widget updates.");
            }

            var beforeValue = GetStringValue(beforeNode) ?? "";
            var afterValue = GetStringValue(afterNode) ?? "";

            if (beforeValue != afterValue)
            {
                if (!afterValue.StartsWith(beforeValue, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Node {id} was updated with a new value that is not a prefix of the initial value. " +
                        "All widget updates must be cumulative.");
                }

                var done = !IsStreaming(afterNode);
                deltas.Add(new WidgetStreamingTextValueDelta
                {
                    ComponentId = id,
                    Delta = afterValue[beforeValue.Length..],
                    Done = done,
                });
            }
        }

        return deltas;
    }

    /// <summary>
    /// Stream a single widget root as a <see cref="ThreadItemDoneEvent"/>.
    /// </summary>
    public static async IAsyncEnumerable<ThreadStreamEvent> StreamWidgetAsync(
        ThreadMetadata thread,
        WidgetRoot widget,
        string? copyText = null,
        Func<StoreItemType, string>? generateId = null,
        TimeProvider? timeProvider = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var clock = timeProvider ?? TimeProvider.System;
        var id = (generateId ?? (t => StoreIdGenerator.GenerateId(t)))(StoreItemType.Message);

        yield return new ThreadItemDoneEvent
        {
            Item = new WidgetItem
            {
                Id = id,
                ThreadId = thread.Id,
                CreatedAt = clock.GetUtcNow().UtcDateTime,
                Widget = widget,
                CopyText = copyText,
            },
        };

        await Task.CompletedTask;
    }

    /// <summary>
    /// Stream an async sequence of widget roots as <see cref="ThreadStreamEvent"/> instances,
    /// computing deltas between successive states.
    /// </summary>
    public static async IAsyncEnumerable<ThreadStreamEvent> StreamWidgetAsync(
        ThreadMetadata thread,
        IAsyncEnumerable<WidgetRoot> widgetStream,
        string? copyText = null,
        Func<StoreItemType, string>? generateId = null,
        TimeProvider? timeProvider = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var clock = timeProvider ?? TimeProvider.System;
        var id = (generateId ?? (t => StoreIdGenerator.GenerateId(t)))(StoreItemType.Message);

        await using var enumerator = widgetStream.GetAsyncEnumerator(ct);

        if (!await enumerator.MoveNextAsync())
            yield break;

        var initialState = enumerator.Current;

        var item = new WidgetItem
        {
            Id = id,
            ThreadId = thread.Id,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
            Widget = initialState,
            CopyText = copyText,
        };

        yield return new ThreadItemAddedEvent { Item = item };

        var lastState = initialState;

        while (await enumerator.MoveNextAsync())
        {
            var newState = enumerator.Current;
            foreach (var update in DiffWidget(lastState, newState))
            {
                yield return new ThreadItemUpdatedEvent
                {
                    ItemId = id,
                    Update = update,
                };
            }
            lastState = newState;
        }

        yield return new ThreadItemDoneEvent
        {
            Item = item with { Widget = lastState },
        };
    }

    // -- Private helpers --

    private static bool IsStreamingText(WidgetComponentBase component) =>
        component is Markdown or Text;

    private static string? GetStringValue(WidgetComponentBase component) =>
        component switch
        {
            Markdown md => md.Value,
            Text txt => txt.Value,
            _ => null,
        };

    private static bool IsStreaming(WidgetComponentBase component) =>
        component switch
        {
            Markdown md => md.Streaming ?? false,
            Text txt => txt.Streaming ?? false,
            _ => false,
        };

    private static bool FullReplace(WidgetComponentBase before, WidgetComponentBase after)
    {
        if (before.GetType() != after.GetType())
            return true;
        if (before.Id != after.Id)
            return true;
        if (before.Key != after.Key)
            return true;

        var beforeJson = JsonSerializer.SerializeToUtf8Bytes(before, before.GetType(), ChatKitJsonOptions.Default);
        var afterJson = JsonSerializer.SerializeToUtf8Bytes(after, after.GetType(), ChatKitJsonOptions.Default);

        using var beforeDoc = JsonDocument.Parse(beforeJson);
        using var afterDoc = JsonDocument.Parse(afterJson);

        var beforeRoot = beforeDoc.RootElement;
        var afterRoot = afterDoc.RootElement;

        var bothStreamingText = IsStreamingText(before) && IsStreamingText(after);

        foreach (var prop in beforeRoot.EnumerateObject())
        {
            // Skip the value field for streaming text -- delta handling covers it
            if (bothStreamingText && prop.Name == "value")
            {
                var afterValueStr = GetStringValue(after) ?? "";
                var beforeValueStr = GetStringValue(before) ?? "";
                if (afterValueStr.StartsWith(beforeValueStr, StringComparison.Ordinal))
                    continue;
            }

            if (!afterRoot.TryGetProperty(prop.Name, out var afterProp))
                return true;

            if (!JsonElementEquals(prop.Value, afterProp))
                return true;
        }

        // Check for new properties in after that were not in before
        foreach (var prop in afterRoot.EnumerateObject())
        {
            if (bothStreamingText && prop.Name == "value")
                continue;

            if (!beforeRoot.TryGetProperty(prop.Name, out _))
                return true;
        }

        return false;
    }

    private static bool JsonElementEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
            return false;

        return a.ValueKind switch
        {
            JsonValueKind.Object => ObjectEquals(a, b),
            JsonValueKind.Array => ArrayEquals(a, b),
            JsonValueKind.String => a.GetString() == b.GetString(),
            JsonValueKind.Number => a.GetRawText() == b.GetRawText(),
            JsonValueKind.True => true,
            JsonValueKind.False => true,
            JsonValueKind.Null => true,
            _ => a.GetRawText() == b.GetRawText(),
        };
    }

    private static bool ObjectEquals(JsonElement a, JsonElement b)
    {
        var aProps = new Dictionary<string, JsonElement>();
        foreach (var prop in a.EnumerateObject())
            aProps[prop.Name] = prop.Value;

        var bProps = new Dictionary<string, JsonElement>();
        foreach (var prop in b.EnumerateObject())
            bProps[prop.Name] = prop.Value;

        if (aProps.Count != bProps.Count)
            return false;

        foreach (var (key, aVal) in aProps)
        {
            if (!bProps.TryGetValue(key, out var bVal))
                return false;
            if (!JsonElementEquals(aVal, bVal))
                return false;
        }

        return true;
    }

    private static bool ArrayEquals(JsonElement a, JsonElement b)
    {
        var aLen = a.GetArrayLength();
        var bLen = b.GetArrayLength();
        if (aLen != bLen)
            return false;

        using var aEnum = a.EnumerateArray();
        using var bEnum = b.EnumerateArray();

        while (aEnum.MoveNext() && bEnum.MoveNext())
        {
            if (!JsonElementEquals(aEnum.Current, bEnum.Current))
                return false;
        }

        return true;
    }

    private static Dictionary<string, WidgetComponentBase> FindAllStreamingTextComponents(
        WidgetComponentBase component)
    {
        var result = new Dictionary<string, WidgetComponentBase>();
        CollectStreamingTextComponents(component, result);
        return result;
    }

    private static void CollectStreamingTextComponents(
        WidgetComponentBase component,
        Dictionary<string, WidgetComponentBase> result)
    {
        if (IsStreamingText(component) && component.Id is not null)
            result[component.Id] = component;

        var children = GetChildren(component);
        if (children is null)
            return;

        foreach (var child in children)
            CollectStreamingTextComponents(child, result);
    }

    private static IEnumerable<WidgetComponentBase>? GetChildren(WidgetComponentBase component) =>
        component switch
        {
            Card card => card.Children,
            ListView listView => listView.Children,
            BoxLayoutProps box => box.Children,
            Transition transition => transition.Children is not null
                ? [transition.Children]
                : null,
            ListViewItem lvi => lvi.Children,
            _ => null,
        };
}
