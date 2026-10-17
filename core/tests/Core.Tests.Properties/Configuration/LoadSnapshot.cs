using Core.Platform.Configuration;

namespace Core.Tests.Properties.Configuration;

/// <summary>The origin of every value in force as text, so two views are compared whole and a difference reads as a diff.</summary>
internal static class LoadSnapshot
{
    public static string OriginsOf(ConfigurationLoadResult result) =>
        string.Join('\n', result.View.Entries.Select(entry => Line(entry.Key, entry.Origin)));

    public static string Line(string path, ConfigurationOrigin origin) => $"{path} from {origin}";
}
