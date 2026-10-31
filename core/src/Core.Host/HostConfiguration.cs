using Core.Host.Supervision;
using Core.Persistence.Connection;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host;

/// <summary>The keys the host itself declares, and the file it reads them from; the settings file is also the one resource this installation must carry.</summary>
internal static class HostConfiguration
{
    internal const string UrlsPath = SectionName + ":" + UrlsKey;

    internal const string BundledFilePath = SectionName + ":" + BundledFileKey;

    internal const string LogFilePath = SectionName + ":" + LogFileKey;

    private const string SettingsFile = "appsettings.json";
    private const string SectionName = "Host";
    private const string UrlsKey = "Urls";
    private const string BundledFileKey = "BundledFile";
    private const string LogFileKey = "LogFile";

    /// <summary>The settings file beside the binary, whatever the working directory the process was started from.</summary>
    internal static string SettingsPath => Path.Combine(AppContext.BaseDirectory, SettingsFile);

    internal static ConfigurationSection Section { get; } =
        new(
            SectionName,
            [
                ConfigurationKey.WithDefault(UrlsKey, "http://127.0.0.1:5000"),
                ConfigurationKey.WithDefault(BundledFileKey, SettingsFile),
                ConfigurationKey.Optional(LogFileKey),
            ]);

    /// <summary>The database section of a configuration that loaded; none from one that was refused, which has no values to read it from
    /// and would otherwise report every key of the section as missing.</summary>
    internal static DatabaseSettings? DatabaseOf(ConfigurationLoadResult loaded, bool migrating)
    {
        if (loaded.Refused)
        {
            return null;
        }

        return migrating ? DatabaseConfiguration.ReadRequired(loaded.Values) : DatabaseConfiguration.Read(loaded.Values);
    }

    /// <summary>The closed configuration of the host: the file beside it, the process environment and the strict arguments; the migration mode does not require the kind of installation.</summary>
    internal static ConfigurationLoadResult Load(string[] args, bool migrating) =>
        ClosedConfiguration.Load(
            new ConfigurationSchema(
                [
                    Section,
                    migrating ? InstallationConfiguration.SectionWhenMigrating : InstallationConfiguration.Section,
                    LanguageServiceConfiguration.Section,
                    RecognizerConfiguration.Section,
                    DatabaseConfiguration.Section,
                ]),
            new JsonFileSource(SettingsPath),
            EnvironmentSource.FromProcess(),
            new StrictArgumentsSource(args));
}
