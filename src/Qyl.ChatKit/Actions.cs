using System.Text.Json.Serialization;

namespace Qyl.ChatKit;

/// <summary>Whether the action is handled on the client or the server.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Handler>))]
public enum Handler
{
    [JsonStringEnumMemberName("client")]
    Client,

    [JsonStringEnumMemberName("server")]
    Server
}

/// <summary>Visual loading behavior when the action executes.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LoadingBehavior>))]
public enum LoadingBehavior
{
    [JsonStringEnumMemberName("auto")]
    Auto,

    [JsonStringEnumMemberName("none")]
    None,

    [JsonStringEnumMemberName("self")]
    Self,

    [JsonStringEnumMemberName("container")]
    Container
}

/// <summary>Fully resolved action configuration sent over the wire.</summary>
public sealed record ActionConfig
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("payload")]
    public object? Payload { get; init; }

    [JsonPropertyName("handler")]
    public Handler Handler { get; init; } = Handler.Server;

    [JsonPropertyName("loadingBehavior")]
    public LoadingBehavior LoadingBehavior { get; init; } = LoadingBehavior.Auto;

    [JsonPropertyName("streaming")]
    public bool Streaming { get; init; } = true;
}

/// <summary>Generic action carrying a type discriminator and payload.</summary>
public sealed record Action<TType, TPayload>
    where TType : notnull
{
    [JsonPropertyName("type")]
    public required TType Type { get; init; }

    [JsonPropertyName("payload")]
    public TPayload? Payload { get; init; }
}
