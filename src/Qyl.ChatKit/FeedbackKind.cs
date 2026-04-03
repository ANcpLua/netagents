using System.Text.Json.Serialization;

namespace Qyl.ChatKit;

/// <summary>Feedback sentiment for a thread item.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FeedbackKind>))]
public enum FeedbackKind
{
    [JsonStringEnumMemberName("positive")]
    Positive,

    [JsonStringEnumMemberName("negative")]
    Negative
}
