using System.Text.Json.Serialization;

namespace Qyl.ChatKit.Widgets;

/// <summary>Configuration object for the X axis.</summary>
public sealed record XAxisConfig
{
    [JsonPropertyName("dataKey")]
    public required string DataKey { get; init; }

    [JsonPropertyName("hide")]
    public bool? Hide { get; init; }

    [JsonPropertyName("labels")]
    public Dictionary<string, string>? Labels { get; init; }
}

/// <summary>Polymorphic base for chart series definitions.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(BarSeries), "bar")]
[JsonDerivedType(typeof(AreaSeries), "area")]
[JsonDerivedType(typeof(LineSeries), "line")]
public abstract record SeriesBase
{
    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonPropertyName("dataKey")]
    public required string DataKey { get; init; }

    [JsonPropertyName("color")]
    public object? Color { get; init; }
}

/// <summary>A bar series plotted from a numeric dataKey. Supports stacking.</summary>
public sealed record BarSeries : SeriesBase
{
    [JsonPropertyName("stack")]
    public string? Stack { get; init; }
}

/// <summary>An area series plotted from a numeric dataKey. Supports stacking and curves.</summary>
public sealed record AreaSeries : SeriesBase
{
    [JsonPropertyName("stack")]
    public string? Stack { get; init; }

    [JsonPropertyName("curveType")]
    public CurveType? CurveType { get; init; }
}

/// <summary>A line series plotted from a numeric dataKey. Supports curves.</summary>
public sealed record LineSeries : SeriesBase
{
    [JsonPropertyName("curveType")]
    public CurveType? CurveType { get; init; }
}

/// <summary>Data visualization component for bar/line/area charts.</summary>
public sealed record Chart : WidgetComponentBase
{
    [JsonPropertyName("data")]
    public required IReadOnlyList<Dictionary<string, object?>> Data { get; init; }

    [JsonPropertyName("series")]
    public required IReadOnlyList<SeriesBase> Series { get; init; }

    [JsonPropertyName("xAxis")]
    public required object XAxis { get; init; }

    [JsonPropertyName("showYAxis")]
    public bool? ShowYAxis { get; init; }

    [JsonPropertyName("showLegend")]
    public bool? ShowLegend { get; init; }

    [JsonPropertyName("showTooltip")]
    public bool? ShowTooltip { get; init; }

    [JsonPropertyName("barGap")]
    public int? BarGap { get; init; }

    [JsonPropertyName("barCategoryGap")]
    public int? BarCategoryGap { get; init; }

    [JsonPropertyName("flex")]
    public object? Flex { get; init; }

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

    [JsonPropertyName("aspectRatio")]
    public object? AspectRatio { get; init; }
}
