using System.Text.Json.Serialization;

namespace Core.Host;

/// <summary>Which recognizer the core asks, shown by the name the configuration takes.</summary>
internal enum RecognizerKind
{
    /// <summary>The language service the core starts and watches.</summary>
    [JsonStringEnumMemberName("http")]
    Http = 0,

    /// <summary>The fake, which asks no service.</summary>
    [JsonStringEnumMemberName("fake")]
    Fake = 1,
}
