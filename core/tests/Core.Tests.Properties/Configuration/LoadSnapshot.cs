using Core.Platform.Configuration;

namespace Core.Tests.Properties.Configuration;

/// <summary>Every value in force with its origin as text, so a difference reads as a diff; values come from the result, since the view redacts the sensitive ones.</summary>
internal static class LoadSnapshot
{
    public static IEnumerable<string> ShownBy(ConfigurationLoadResult result) =>
        result.View.Entries.Select(entry => Line(entry.Key, result.Values[entry.Key], entry.Origin));

    public static string Line(string path, string value, ConfigurationOrigin origin) => $"{path}={value} from {origin}";
}
