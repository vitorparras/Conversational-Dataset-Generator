using System.Text.Json.Serialization;

namespace Core.Platform.Versions;

/// <summary>
/// A version the system obtained, or the statement that it obtained none. Absent is its own value: it is never an
/// empty text, never a default, and it is written as JSON <c>null</c> where a present version is a string.
/// </summary>
[JsonConverter(typeof(VersionValueJsonConverter))]
public sealed record VersionValue
{
    private const string AbsentMarker = "<absent>";

    private VersionValue(string? text)
    {
        Text = text;
    }

    /// <summary>The version was not obtained.</summary>
    public static VersionValue Absent { get; } = new(text: null);

    /// <summary>The text is null only when the version is absent.</summary>
    internal string? Text { get; }

    /// <summary>A version that was obtained; an empty or blank text is not a version and is refused.</summary>
    public static VersionValue Present(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new VersionValue(value);
    }

    /// <summary>The version itself, or, when it is absent, a marker that no commit or package version spells.</summary>
    public override string ToString() => Text ?? AbsentMarker;
}
