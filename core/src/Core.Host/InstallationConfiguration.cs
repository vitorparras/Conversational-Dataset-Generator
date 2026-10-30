using Core.Platform;
using Core.Platform.Configuration;
using Core.Platform.Versions;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host;

/// <summary>Whether the installation is real or a demonstration: declared by every installation, with no default, since a default would let a real one pass for either.</summary>
internal static class InstallationConfiguration
{
    internal const string KindPath = SectionName + ":" + KindKey;

    private const string SectionName = "Installation";
    private const string KindKey = "Kind";
    private const string Real = "real";
    private const string Demonstration = "demonstracao";

    internal static ConfigurationSection Section { get; } =
        new(SectionName, [ConfigurationKey.Required(KindKey).Accepting(Real, Demonstration)]);

    /// <summary>The same section for the migration mode, which serves nothing: the kind may be given, and is then checked, but is not required.</summary>
    internal static ConfigurationSection SectionWhenMigrating { get; } =
        new(SectionName, [ConfigurationKey.Optional(KindKey).Accepting(Real, Demonstration)]);

    /// <summary>The installation the approved value declares.</summary>
    internal static Installation Of(IReadOnlyDictionary<string, string> values) =>
        new(string.Equals(values[KindPath], Demonstration, StringComparison.Ordinal) ? InstallationKind.Demonstration : InstallationKind.Real);

    /// <summary>The report of versions with the installation the host declares beside the versions the checks confirmed.</summary>
    internal static VersionReport Shown(VersionReport report, IServiceProvider services) =>
        report.WithInstallation(services.GetRequiredService<Installation>());

    /// <summary>Writes, once, the line that says which kind of installation this is.</summary>
    internal static void Announce(ILogger logger, IServiceProvider services)
    {
        var installation = services.GetRequiredService<Installation>();
        if (installation.Kind == InstallationKind.Demonstration)
        {
            logger.DemonstrationInstallation(installation);
        }
        else
        {
            logger.RealInstallation(installation);
        }
    }
}
