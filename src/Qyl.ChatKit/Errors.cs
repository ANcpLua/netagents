using System.Text.Json.Serialization;

namespace Qyl.ChatKit;

/// <summary>
/// Known error codes emitted by the ChatKit stream protocol.
/// Not a closed set -- new codes can be added as needed.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ErrorCode>))]
public enum ErrorCode
{
    [JsonStringEnumMemberName("stream.error")]
    StreamError
}

/// <summary>
/// Error with a specific error code that maps to a localized user-facing error message.
/// </summary>
public sealed class StreamError(ErrorCode code, bool? allowRetry = null)
    : Exception($"Stream error: {code}")
{
    public ErrorCode Code { get; } = code;

    public bool AllowRetry { get; } = allowRetry ?? code switch
    {
        ErrorCode.StreamError => true,
        _ => false
    };
}

/// <summary>
/// Error with a custom user-facing error message. The message should be localized
/// as needed before raising the error.
/// </summary>
public sealed class CustomStreamError(string message, bool allowRetry = false)
    : Exception(message)
{
    public bool AllowRetry { get; } = allowRetry;
}
