using Core.Platform.Configuration;

namespace Core.Tests.Properties.Configuration;

/// <summary>Every value in force with its origin as text, so a difference reads as a diff: as the view shows it, and as the consumer receives it.</summary>
internal static class LoadSnapshot
{
    public static IEnumerable<string> ShownBy(ConfigurationLoadResult result) =>
        result.View.Entries.Select(entry => Line(entry.Key, entry.Value, entry.Origin));

    public static IEnumerable<string> UsedBy(ConfigurationLoadResult result) =>
        result.View.Entries.Select(entry => Line(entry.Key, result.Values[entry.Key], entry.Origin));

    public static string Line(string path, string value, ConfigurationOrigin origin) => $"{path}={value} from {origin}";
}
