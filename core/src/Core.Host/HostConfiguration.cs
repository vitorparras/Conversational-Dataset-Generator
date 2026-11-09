using Core.Host.Supervision;
using Core.Ingestion;
using Core.Persistence.Connection;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using Core.Platform.Startup;
using Core.Platform.Versions;
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
                ConfigurationKey.WithDefault(UrlsKey, "https://127.0.0.1:5000"),
                ConfigurationKey.WithDefault(BundledFileKey, SettingsFile),
                ConfigurationKey.Optional(LogFileKey)
            ]);

    /// <summary>The database section of a configuration that loaded; none from one that was refused, which has no values to read it from
    /// and would otherwise report every key of the section as missing.</summary>
    internal static DatabaseSettings? DatabaseOf(ConfigurationLoadResult loaded)
    {
        if (loaded.Refused)
        {
            return null;
        }

        return DatabaseConfiguration.Read(loaded.Values);
    }

    /// <summary>
    /// The closed configuration of the host: the file beside it, the process environment and the strict arguments;
    /// the migration mode, and the comparison mode that loads as one, require neither the kind of installation nor the custody files.
    /// </summary>
    internal static ConfigurationLoadResult Load(string[] args, bool migrating, bool comparing = false) =>
        ClosedConfiguration.Load(
            new ConfigurationSchema(
                [
                    Section,
                    migrating ? InstallationConfiguration.SectionWhenMigrating : InstallationConfiguration.Section,
                    migrating ? MasterKeyConfiguration.SectionWhenMigrating : MasterKeyConfiguration.Section,
                    migrating ? TlsConfiguration.SectionWhenMigrating : TlsConfiguration.Section,
                    SafekeepingConfiguration.Section,
                    migrating ? PasswordConfiguration.SectionWhenMigrating : PasswordConfiguration.Section,
                    migrating ? SessionConfiguration.SectionWhenMigrating : SessionConfiguration.Section,
                    migrating ? AdmissionConfiguration.SectionWhenMigrating : AdmissionConfiguration.Section,
                    ReplicationConfiguration.Section,
                    migrating ? VerificationConfiguration.SectionWhenMigrating : VerificationConfiguration.Section,
                    migrating ? SealConfiguration.SectionWhenMigrating : SealConfiguration.Section,
                    AuditSection(migrating, comparing),
                    LanguageServiceConfiguration.Section,
                    RecognizerConfiguration.Section,
                    DatabaseConfiguration.Section
                ]),
            new JsonFileSource(SettingsPath),
            EnvironmentSource.FromProcess(),
            new StrictArgumentsSource(args));

    /// <summary>Runs before the configuration is read: an executable built for another target is refused whatever it is configured with.</summary>
    internal static RuntimeTargetCheck TargetCheck() =>
        new(BuildInfo.Of(typeof(HostConfiguration).Assembly).RuntimeIdentifier, RuntimeTarget.Current);

    private static ConfigurationSection AuditSection(bool migrating, bool comparing)
    {
        if (comparing)
        {
            return AuditConfiguration.SectionWhenComparing;
        }

        return migrating ? AuditConfiguration.SectionWhenMigrating : AuditConfiguration.Section;
    }
}
