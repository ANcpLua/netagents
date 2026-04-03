using System.Text.Json.Serialization;

namespace Qyl.ChatKit.Widgets;

/// <summary>Shared layout properties for flexible container widgets (Box, Row, Col, Form).</summary>
public abstract record BoxLayoutProps : WidgetComponentBase
{
    [JsonPropertyName("children")]
    public IReadOnlyList<WidgetComponentBase>? Children { get; init; }

    [JsonPropertyName("align")]
    public Alignment? Align { get; init; }

    [JsonPropertyName("justify")]
    public Justification? Justify { get; init; }

    [JsonPropertyName("wrap")]
    public string? Wrap { get; init; }

    [JsonPropertyName("flex")]
    public object? Flex { get; init; }

    [JsonPropertyName("gap")]
    public object? Gap { get; init; }

    [JsonPropertyName("height")]
    public object? Height { get; init; }

    [JsonPropertyName("width")]
    public object? Width { get; init; }

    [JsonPropertyName("size")]
    public object? Size { get; init; }

    [JsonPropertyName("minHeight")]
    public object? MinHeight { get; init; }

    [JsonPropertyName("minWidth")]
    public object? MinWidth { get; init; }

    [JsonPropertyName("minSize")]
    public object? MinSize { get; init; }

    [JsonPropertyName("maxHeight")]
    public object? MaxHeight { get; init; }

    [JsonPropertyName("maxWidth")]
    public object? MaxWidth { get; init; }

    [JsonPropertyName("maxSize")]
    public object? MaxSize { get; init; }

    [JsonPropertyName("padding")]
    public object? Padding { get; init; }

    [JsonPropertyName("margin")]
    public object? Margin { get; init; }

    [JsonPropertyName("border")]
    public object? Border { get; init; }

    [JsonPropertyName("radius")]
    public RadiusValue? Radius { get; init; }

    [JsonPropertyName("background")]
    public object? Background { get; init; }

    [JsonPropertyName("aspectRatio")]
    public object? AspectRatio { get; init; }
}

/// <summary>Plain text with typography controls.</summary>
public sealed record Text : WidgetComponentBase
{
    [JsonPropertyName("value")]
    public required string Value { get; init; }

    [JsonPropertyName("streaming")]
    public bool? Streaming { get; init; }

    [JsonPropertyName("italic")]
    public bool? Italic { get; init; }

    [JsonPropertyName("lineThrough")]
    public bool? LineThrough { get; init; }

    [JsonPropertyName("color")]
    public object? Color { get; init; }

    [JsonPropertyName("weight")]
    public string? Weight { get; init; }

    [JsonPropertyName("width")]
    public object? Width { get; init; }

    [JsonPropertyName("size")]
    public TextSize? Size { get; init; }

    [JsonPropertyName("textAlign")]
    public TextAlign? TextAlign { get; init; }

    [JsonPropertyName("truncate")]
    public bool? Truncate { get; init; }

    [JsonPropertyName("minLines")]
    public int? MinLines { get; init; }

    [JsonPropertyName("maxLines")]
    public int? MaxLines { get; init; }

    [JsonPropertyName("editable")]
    public object? Editable { get; init; }
}

/// <summary>Prominent headline text.</summary>
public sealed record Title : WidgetComponentBase
{
    [JsonPropertyName("value")]
    public required string Value { get; init; }

    [JsonPropertyName("color")]
    public object? Color { get; init; }

    [JsonPropertyName("weight")]
    public string? Weight { get; init; }

    [JsonPropertyName("size")]
    public TitleSize? Size { get; init; }

    [JsonPropertyName("textAlign")]
    public TextAlign? TextAlign { get; init; }

    [JsonPropertyName("truncate")]
    public bool? Truncate { get; init; }

    [JsonPropertyName("maxLines")]
    public int? MaxLines { get; init; }
}

/// <summary>Supporting caption text.</summary>
public sealed record Caption : WidgetComponentBase
{
    [JsonPropertyName("value")]
    public required string Value { get; init; }

    [JsonPropertyName("color")]
    public object? Color { get; init; }

    [JsonPropertyName("weight")]
    public string? Weight { get; init; }

    [JsonPropertyName("size")]
    public CaptionSize? Size { get; init; }

    [JsonPropertyName("textAlign")]
    public TextAlign? TextAlign { get; init; }

    [JsonPropertyName("truncate")]
    public bool? Truncate { get; init; }

    [JsonPropertyName("maxLines")]
    public int? MaxLines { get; init; }
}

/// <summary>Markdown content, optionally streamed.</summary>
public sealed record Markdown : WidgetComponentBase
{
    [JsonPropertyName("value")]
    public required string Value { get; init; }

    [JsonPropertyName("streaming")]
    public bool? Streaming { get; init; }
}

/// <summary>Small badge indicating status or categorization.</summary>
public sealed record Badge : WidgetComponentBase
{
    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("color")]
    public string? Color { get; init; }

    [JsonPropertyName("variant")]
    public string? Variant { get; init; }

    [JsonPropertyName("size")]
    public string? Size { get; init; }

    [JsonPropertyName("pill")]
    public bool? Pill { get; init; }
}

/// <summary>Generic flex container with direction control.</summary>
public sealed record Box : BoxLayoutProps
{
    [JsonPropertyName("direction")]
    public string? Direction { get; init; }
}

/// <summary>Horizontal flex container.</summary>
public sealed record Row : BoxLayoutProps;

/// <summary>Vertical flex container.</summary>
public sealed record Col : BoxLayoutProps;

/// <summary>Form wrapper capable of submitting onSubmitAction.</summary>
public sealed record Form : BoxLayoutProps
{
    [JsonPropertyName("onSubmitAction")]
    public ActionConfig? OnSubmitAction { get; init; }

    [JsonPropertyName("direction")]
    public string? Direction { get; init; }
}

/// <summary>Visual divider separating content sections.</summary>
public sealed record Divider : WidgetComponentBase
{
    [JsonPropertyName("color")]
    public object? Color { get; init; }

    [JsonPropertyName("size")]
    public object? Size { get; init; }

    [JsonPropertyName("spacing")]
    public object? Spacing { get; init; }

    [JsonPropertyName("flush")]
    public bool? Flush { get; init; }
}

/// <summary>Icon component referencing a built-in icon name.</summary>
public sealed record Icon : WidgetComponentBase
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("color")]
    public object? Color { get; init; }

    [JsonPropertyName("size")]
    public IconSize? Size { get; init; }
}

/// <summary>Image component with sizing and fitting controls.</summary>
public sealed record Image : WidgetComponentBase
{
    [JsonPropertyName("src")]
    public required string Src { get; init; }

    [JsonPropertyName("alt")]
    public string? Alt { get; init; }

    [JsonPropertyName("fit")]
    public string? Fit { get; init; }

    [JsonPropertyName("position")]
    public string? Position { get; init; }

    [JsonPropertyName("radius")]
    public RadiusValue? Radius { get; init; }

    [JsonPropertyName("frame")]
    public bool? Frame { get; init; }

    [JsonPropertyName("flush")]
    public bool? Flush { get; init; }

    [JsonPropertyName("height")]
    public object? Height { get; init; }

    [JsonPropertyName("width")]
    public object? Width { get; init; }

    [JsonPropertyName("size")]
    public object? Size { get; init; }

    [JsonPropertyName("minHeight")]
    public object? MinHeight { get; init; }

    [JsonPropertyName("minWidth")]
    public object? MinWidth { get; init; }

    [JsonPropertyName("minSize")]
    public object? MinSize { get; init; }

    [JsonPropertyName("maxHeight")]
    public object? MaxHeight { get; init; }

    [JsonPropertyName("maxWidth")]
    public object? MaxWidth { get; init; }

    [JsonPropertyName("maxSize")]
    public object? MaxSize { get; init; }

    [JsonPropertyName("margin")]
    public object? Margin { get; init; }

    [JsonPropertyName("background")]
    public object? Background { get; init; }

    [JsonPropertyName("aspectRatio")]
    public object? AspectRatio { get; init; }

    [JsonPropertyName("flex")]
    public object? Flex { get; init; }
}

/// <summary>Button component optionally wired to an action.</summary>
public sealed record Button : WidgetComponentBase
{
    [JsonPropertyName("submit")]
    public bool? Submit { get; init; }

    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonPropertyName("onClickAction")]
    public ActionConfig? OnClickAction { get; init; }

    [JsonPropertyName("iconStart")]
    public string? IconStart { get; init; }

    [JsonPropertyName("iconEnd")]
    public string? IconEnd { get; init; }

    [JsonPropertyName("style")]
    public string? Style { get; init; }

    [JsonPropertyName("iconSize")]
    public string? IconSize { get; init; }

    [JsonPropertyName("color")]
    public string? Color { get; init; }

    [JsonPropertyName("variant")]
    public ControlVariant? Variant { get; init; }

    [JsonPropertyName("size")]
    public ControlSize? Size { get; init; }

    [JsonPropertyName("pill")]
    public bool? Pill { get; init; }

    [JsonPropertyName("uniform")]
    public bool? Uniform { get; init; }

    [JsonPropertyName("block")]
    public bool? Block { get; init; }

    [JsonPropertyName("disabled")]
    public bool? Disabled { get; init; }
}

/// <summary>Flexible spacer used to push content apart.</summary>
public sealed record Spacer : WidgetComponentBase
{
    [JsonPropertyName("minSize")]
    public object? MinSize { get; init; }
}

/// <summary>Select dropdown component.</summary>
public sealed record Select : WidgetComponentBase
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("options")]
    public required IReadOnlyList<SelectOption> Options { get; init; }

    [JsonPropertyName("onChangeAction")]
    public ActionConfig? OnChangeAction { get; init; }

    [JsonPropertyName("placeholder")]
    public string? Placeholder { get; init; }

    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; init; }

    [JsonPropertyName("variant")]
    public ControlVariant? Variant { get; init; }

    [JsonPropertyName("size")]
    public ControlSize? Size { get; init; }

    [JsonPropertyName("pill")]
    public bool? Pill { get; init; }

    [JsonPropertyName("block")]
    public bool? Block { get; init; }

    [JsonPropertyName("clearable")]
    public bool? Clearable { get; init; }

    [JsonPropertyName("disabled")]
    public bool? Disabled { get; init; }

    [JsonPropertyName("searchable")]
    public bool? Searchable { get; init; }
}

/// <summary>Date picker input component.</summary>
public sealed record DatePicker : WidgetComponentBase
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("onChangeAction")]
    public ActionConfig? OnChangeAction { get; init; }

    [JsonPropertyName("placeholder")]
    public string? Placeholder { get; init; }

    [JsonPropertyName("defaultValue")]
    public DateTimeOffset? DefaultValue { get; init; }

    [JsonPropertyName("min")]
    public DateTimeOffset? Min { get; init; }

    [JsonPropertyName("max")]
    public DateTimeOffset? Max { get; init; }

    [JsonPropertyName("variant")]
    public ControlVariant? Variant { get; init; }

    [JsonPropertyName("size")]
    public ControlSize? Size { get; init; }

    [JsonPropertyName("side")]
    public string? Side { get; init; }

    [JsonPropertyName("align")]
    public string? Align { get; init; }

    [JsonPropertyName("pill")]
    public bool? Pill { get; init; }

    [JsonPropertyName("block")]
    public bool? Block { get; init; }

    [JsonPropertyName("clearable")]
    public bool? Clearable { get; init; }

    [JsonPropertyName("disabled")]
    public bool? Disabled { get; init; }
}

/// <summary>Checkbox input component.</summary>
public sealed record Checkbox : WidgetComponentBase
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonPropertyName("defaultChecked")]
    public bool? DefaultChecked { get; init; }

    [JsonPropertyName("onChangeAction")]
    public ActionConfig? OnChangeAction { get; init; }

    [JsonPropertyName("disabled")]
    public bool? Disabled { get; init; }

    [JsonPropertyName("required")]
    public bool? Required { get; init; }
}

/// <summary>Single-line text input component.</summary>
public sealed record Input : WidgetComponentBase
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("inputType")]
    public string? InputType { get; init; }

    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; init; }

    [JsonPropertyName("required")]
    public bool? Required { get; init; }

    [JsonPropertyName("pattern")]
    public string? Pattern { get; init; }

    [JsonPropertyName("placeholder")]
    public string? Placeholder { get; init; }

    [JsonPropertyName("allowAutofillExtensions")]
    public bool? AllowAutofillExtensions { get; init; }

    [JsonPropertyName("autoSelect")]
    public bool? AutoSelect { get; init; }

    [JsonPropertyName("autoFocus")]
    public bool? AutoFocus { get; init; }

    [JsonPropertyName("disabled")]
    public bool? Disabled { get; init; }

    [JsonPropertyName("variant")]
    public string? Variant { get; init; }

    [JsonPropertyName("size")]
    public ControlSize? Size { get; init; }

    [JsonPropertyName("gutterSize")]
    public string? GutterSize { get; init; }

    [JsonPropertyName("pill")]
    public bool? Pill { get; init; }
}

/// <summary>Form label associated with a field.</summary>
public sealed record Label : WidgetComponentBase
{
    [JsonPropertyName("value")]
    public required string Value { get; init; }

    [JsonPropertyName("fieldName")]
    public required string FieldName { get; init; }

    [JsonPropertyName("size")]
    public TextSize? Size { get; init; }

    [JsonPropertyName("weight")]
    public string? Weight { get; init; }

    [JsonPropertyName("textAlign")]
    public TextAlign? TextAlign { get; init; }

    [JsonPropertyName("color")]
    public object? Color { get; init; }
}

/// <summary>Grouped radio input control.</summary>
public sealed record RadioGroup : WidgetComponentBase
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("options")]
    public IReadOnlyList<RadioOption>? Options { get; init; }

    [JsonPropertyName("ariaLabel")]
    public string? AriaLabel { get; init; }

    [JsonPropertyName("onChangeAction")]
    public ActionConfig? OnChangeAction { get; init; }

    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; init; }

    [JsonPropertyName("direction")]
    public string? Direction { get; init; }

    [JsonPropertyName("disabled")]
    public bool? Disabled { get; init; }

    [JsonPropertyName("required")]
    public bool? Required { get; init; }
}

/// <summary>Multiline text input component.</summary>
public sealed record Textarea : WidgetComponentBase
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; init; }

    [JsonPropertyName("required")]
    public bool? Required { get; init; }

    [JsonPropertyName("pattern")]
    public string? Pattern { get; init; }

    [JsonPropertyName("placeholder")]
    public string? Placeholder { get; init; }

    [JsonPropertyName("autoSelect")]
    public bool? AutoSelect { get; init; }

    [JsonPropertyName("autoFocus")]
    public bool? AutoFocus { get; init; }

    [JsonPropertyName("disabled")]
    public bool? Disabled { get; init; }

    [JsonPropertyName("variant")]
    public string? Variant { get; init; }

    [JsonPropertyName("size")]
    public ControlSize? Size { get; init; }

    [JsonPropertyName("gutterSize")]
    public string? GutterSize { get; init; }

    [JsonPropertyName("rows")]
    public int? Rows { get; init; }

    [JsonPropertyName("autoResize")]
    public bool? AutoResize { get; init; }

    [JsonPropertyName("maxRows")]
    public int? MaxRows { get; init; }

    [JsonPropertyName("allowAutofillExtensions")]
    public bool? AllowAutofillExtensions { get; init; }
}

/// <summary>Wrapper enabling transitions for a child component.</summary>
public sealed record Transition : WidgetComponentBase
{
    [JsonPropertyName("children")]
    public WidgetComponentBase? Children { get; init; }
}

/// <summary>Single row inside a ListView component.</summary>
public sealed record ListViewItem : WidgetComponentBase
{
    [JsonPropertyName("children")]
    public required IReadOnlyList<WidgetComponentBase> Children { get; init; }

    [JsonPropertyName("onClickAction")]
    public ActionConfig? OnClickAction { get; init; }

    [JsonPropertyName("gap")]
    public object? Gap { get; init; }

    [JsonPropertyName("align")]
    public Alignment? Align { get; init; }
}
