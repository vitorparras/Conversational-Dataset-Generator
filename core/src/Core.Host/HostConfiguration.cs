using Core.Platform.Configuration;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host;

/// <summary>The keys the host itself declares, and the file it reads them from.</summary>
internal static class HostConfiguration
{
    public const string UrlsPath = SectionName + ":" + UrlsKey;

    private const string SettingsFile = "appsettings.json";
    private const string SectionName = "Host";
    private const string UrlsKey = "Urls";

    /// <summary>The settings file beside the binary, whatever the working directory the process was started from.</summary>
    public static string SettingsPath => Path.Combine(AppContext.BaseDirectory, SettingsFile);

    public static ConfigurationSection Section { get; } =
        new(SectionName, [ConfigurationKey.WithDefault(UrlsKey, "http://127.0.0.1:5000")]);
}
