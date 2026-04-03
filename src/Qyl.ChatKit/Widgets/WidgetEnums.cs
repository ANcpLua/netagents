using System.Text.Json.Serialization;

namespace Qyl.ChatKit.Widgets;

/// <summary>Allowed corner radius tokens.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RadiusValue>))]
public enum RadiusValue
{
    [JsonStringEnumMemberName("2xs")]
    TwoXs,

    [JsonStringEnumMemberName("xs")]
    Xs,

    [JsonStringEnumMemberName("sm")]
    Sm,

    [JsonStringEnumMemberName("md")]
    Md,

    [JsonStringEnumMemberName("lg")]
    Lg,

    [JsonStringEnumMemberName("xl")]
    Xl,

    [JsonStringEnumMemberName("2xl")]
    TwoXl,

    [JsonStringEnumMemberName("3xl")]
    ThreeXl,

    [JsonStringEnumMemberName("4xl")]
    FourXl,

    [JsonStringEnumMemberName("full")]
    Full,

    [JsonStringEnumMemberName("100%")]
    HundredPercent,

    [JsonStringEnumMemberName("none")]
    None
}

/// <summary>Horizontal text alignment options.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TextAlign>))]
public enum TextAlign
{
    [JsonStringEnumMemberName("start")]
    Start,

    [JsonStringEnumMemberName("center")]
    Center,

    [JsonStringEnumMemberName("end")]
    End
}

/// <summary>Body text size tokens.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TextSize>))]
public enum TextSize
{
    [JsonStringEnumMemberName("xs")]
    Xs,

    [JsonStringEnumMemberName("sm")]
    Sm,

    [JsonStringEnumMemberName("md")]
    Md,

    [JsonStringEnumMemberName("lg")]
    Lg,

    [JsonStringEnumMemberName("xl")]
    Xl
}

/// <summary>Title text size tokens.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TitleSize>))]
public enum TitleSize
{
    [JsonStringEnumMemberName("sm")]
    Sm,

    [JsonStringEnumMemberName("md")]
    Md,

    [JsonStringEnumMemberName("lg")]
    Lg,

    [JsonStringEnumMemberName("xl")]
    Xl,

    [JsonStringEnumMemberName("2xl")]
    TwoXl,

    [JsonStringEnumMemberName("3xl")]
    ThreeXl,

    [JsonStringEnumMemberName("4xl")]
    FourXl,

    [JsonStringEnumMemberName("5xl")]
    FiveXl
}

/// <summary>Caption text size tokens.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CaptionSize>))]
public enum CaptionSize
{
    [JsonStringEnumMemberName("sm")]
    Sm,

    [JsonStringEnumMemberName("md")]
    Md,

    [JsonStringEnumMemberName("lg")]
    Lg
}

/// <summary>Icon size tokens.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<IconSize>))]
public enum IconSize
{
    [JsonStringEnumMemberName("xs")]
    Xs,

    [JsonStringEnumMemberName("sm")]
    Sm,

    [JsonStringEnumMemberName("md")]
    Md,

    [JsonStringEnumMemberName("lg")]
    Lg,

    [JsonStringEnumMemberName("xl")]
    Xl,

    [JsonStringEnumMemberName("2xl")]
    TwoXl,

    [JsonStringEnumMemberName("3xl")]
    ThreeXl
}

/// <summary>Flexbox alignment options.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Alignment>))]
public enum Alignment
{
    [JsonStringEnumMemberName("start")]
    Start,

    [JsonStringEnumMemberName("center")]
    Center,

    [JsonStringEnumMemberName("end")]
    End,

    [JsonStringEnumMemberName("baseline")]
    Baseline,

    [JsonStringEnumMemberName("stretch")]
    Stretch
}

/// <summary>Flexbox justification options.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Justification>))]
public enum Justification
{
    [JsonStringEnumMemberName("start")]
    Start,

    [JsonStringEnumMemberName("center")]
    Center,

    [JsonStringEnumMemberName("end")]
    End,

    [JsonStringEnumMemberName("between")]
    Between,

    [JsonStringEnumMemberName("around")]
    Around,

    [JsonStringEnumMemberName("evenly")]
    Evenly,

    [JsonStringEnumMemberName("stretch")]
    Stretch
}

/// <summary>Button and input style variants.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ControlVariant>))]
public enum ControlVariant
{
    [JsonStringEnumMemberName("solid")]
    Solid,

    [JsonStringEnumMemberName("soft")]
    Soft,

    [JsonStringEnumMemberName("outline")]
    Outline,

    [JsonStringEnumMemberName("ghost")]
    Ghost
}

/// <summary>Button and input size variants.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ControlSize>))]
public enum ControlSize
{
    [JsonStringEnumMemberName("3xs")]
    ThreeXs,

    [JsonStringEnumMemberName("2xs")]
    TwoXs,

    [JsonStringEnumMemberName("xs")]
    Xs,

    [JsonStringEnumMemberName("sm")]
    Sm,

    [JsonStringEnumMemberName("md")]
    Md,

    [JsonStringEnumMemberName("lg")]
    Lg,

    [JsonStringEnumMemberName("xl")]
    Xl,

    [JsonStringEnumMemberName("2xl")]
    TwoXl,

    [JsonStringEnumMemberName("3xl")]
    ThreeXl
}

/// <summary>Interpolation curve types for area and line series.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CurveType>))]
public enum CurveType
{
    [JsonStringEnumMemberName("basis")]
    Basis,

    [JsonStringEnumMemberName("basisClosed")]
    BasisClosed,

    [JsonStringEnumMemberName("basisOpen")]
    BasisOpen,

    [JsonStringEnumMemberName("bumpX")]
    BumpX,

    [JsonStringEnumMemberName("bumpY")]
    BumpY,

    [JsonStringEnumMemberName("bump")]
    Bump,

    [JsonStringEnumMemberName("linear")]
    Linear,

    [JsonStringEnumMemberName("linearClosed")]
    LinearClosed,

    [JsonStringEnumMemberName("natural")]
    Natural,

    [JsonStringEnumMemberName("monotoneX")]
    MonotoneX,

    [JsonStringEnumMemberName("monotoneY")]
    MonotoneY,

    [JsonStringEnumMemberName("monotone")]
    Monotone,

    [JsonStringEnumMemberName("step")]
    Step,

    [JsonStringEnumMemberName("stepBefore")]
    StepBefore,

    [JsonStringEnumMemberName("stepAfter")]
    StepAfter
}
