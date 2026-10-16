using Core.Platform.Configuration;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host;

/// <summary>The keys the host itself declares, and the file it reads them from; the settings file is also the one resource this installation must carry.</summary>
internal static class HostConfiguration
{
    internal const string UrlsPath = SectionName + ":" + UrlsKey;

    internal const string BundledFilePath = SectionName + ":" + BundledFileKey;

    private const string SettingsFile = "appsettings.json";
    private const string SectionName = "Host";
    private const string UrlsKey = "Urls";
    private const string BundledFileKey = "BundledFile";

    /// <summary>The settings file beside the binary, whatever the working directory the process was started from.</summary>
    internal static string SettingsPath => Path.Combine(AppContext.BaseDirectory, SettingsFile);

    internal static ConfigurationSection Section { get; } =
        new(
            SectionName,
            [
                ConfigurationKey.WithDefault(UrlsKey, "http://127.0.0.1:5000"),
                ConfigurationKey.WithDefault(BundledFileKey, SettingsFile),
            ]);
}
