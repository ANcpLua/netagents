using System.Text.Json.Serialization;

namespace Qyl.ChatKit.Widgets;

/// <summary>Color values for light and dark themes.</summary>
public sealed record ThemeColor
{
    [JsonPropertyName("dark")]
    public required string Dark { get; init; }

    [JsonPropertyName("light")]
    public required string Light { get; init; }
}

/// <summary>Shorthand spacing values applied to a widget.</summary>
public sealed record Spacing
{
    [JsonPropertyName("top")]
    public object? Top { get; init; }

    [JsonPropertyName("right")]
    public object? Right { get; init; }

    [JsonPropertyName("bottom")]
    public object? Bottom { get; init; }

    [JsonPropertyName("left")]
    public object? Left { get; init; }

    [JsonPropertyName("x")]
    public object? X { get; init; }

    [JsonPropertyName("y")]
    public object? Y { get; init; }
}

/// <summary>Border style definition for an edge.</summary>
public sealed record Border
{
    [JsonPropertyName("size")]
    public required int Size { get; init; }

    [JsonPropertyName("color")]
    public object? Color { get; init; }

    [JsonPropertyName("style")]
    public string? Style { get; init; }
}

/// <summary>Composite border configuration applied across edges.</summary>
public sealed record Borders
{
    [JsonPropertyName("top")]
    public object? Top { get; init; }

    [JsonPropertyName("right")]
    public object? Right { get; init; }

    [JsonPropertyName("bottom")]
    public object? Bottom { get; init; }

    [JsonPropertyName("left")]
    public object? Left { get; init; }

    [JsonPropertyName("x")]
    public object? X { get; init; }

    [JsonPropertyName("y")]
    public object? Y { get; init; }
}

/// <summary>Editable field options for text widgets.</summary>
public sealed record EditableProps
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("autoFocus")]
    public bool? AutoFocus { get; init; }

    [JsonPropertyName("autoSelect")]
    public bool? AutoSelect { get; init; }

    [JsonPropertyName("autoComplete")]
    public string? AutoComplete { get; init; }

    [JsonPropertyName("allowAutofillExtensions")]
    public bool? AllowAutofillExtensions { get; init; }

    [JsonPropertyName("pattern")]
    public string? Pattern { get; init; }

    [JsonPropertyName("placeholder")]
    public string? Placeholder { get; init; }

    [JsonPropertyName("required")]
    public bool? Required { get; init; }
}

/// <summary>Selectable option used by the Select widget.</summary>
public sealed record SelectOption
{
    [JsonPropertyName("value")]
    public required string Value { get; init; }

    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("disabled")]
    public bool? Disabled { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>Option inside a RadioGroup widget.</summary>
public sealed record RadioOption
{
    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("value")]
    public required string Value { get; init; }

    [JsonPropertyName("disabled")]
    public bool? Disabled { get; init; }
}

/// <summary>Configuration for confirm/cancel actions within a card.</summary>
public sealed record CardAction
{
    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("action")]
    public required ActionConfig Action { get; init; }
}

/// <summary>Base model for all widget components.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Text), "Text")]
[JsonDerivedType(typeof(Title), "Title")]
[JsonDerivedType(typeof(Caption), "Caption")]
[JsonDerivedType(typeof(Markdown), "Markdown")]
[JsonDerivedType(typeof(Badge), "Badge")]
[JsonDerivedType(typeof(Box), "Box")]
[JsonDerivedType(typeof(Row), "Row")]
[JsonDerivedType(typeof(Col), "Col")]
[JsonDerivedType(typeof(Form), "Form")]
[JsonDerivedType(typeof(Divider), "Divider")]
[JsonDerivedType(typeof(Icon), "Icon")]
[JsonDerivedType(typeof(Image), "Image")]
[JsonDerivedType(typeof(Button), "Button")]
[JsonDerivedType(typeof(Spacer), "Spacer")]
[JsonDerivedType(typeof(Select), "Select")]
[JsonDerivedType(typeof(DatePicker), "DatePicker")]
[JsonDerivedType(typeof(Checkbox), "Checkbox")]
[JsonDerivedType(typeof(Input), "Input")]
[JsonDerivedType(typeof(Label), "Label")]
[JsonDerivedType(typeof(RadioGroup), "RadioGroup")]
[JsonDerivedType(typeof(Textarea), "Textarea")]
[JsonDerivedType(typeof(Transition), "Transition")]
[JsonDerivedType(typeof(Chart), "Chart")]
[JsonDerivedType(typeof(ListViewItem), "ListViewItem")]
[JsonDerivedType(typeof(Card), "Card")]
[JsonDerivedType(typeof(ListView), "ListView")]
[JsonDerivedType(typeof(BasicRoot), "Basic")]
public abstract record WidgetComponentBase
{
    [JsonPropertyName("key")]
    public string? Key { get; init; }

    [JsonPropertyName("id")]
    public string? Id { get; init; }
}

/// <summary>Polymorphic base for top-level widget roots.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Card), "Card")]
[JsonDerivedType(typeof(ListView), "ListView")]
[JsonDerivedType(typeof(BasicRoot), "Basic")]
public abstract record WidgetRoot : WidgetComponentBase;
