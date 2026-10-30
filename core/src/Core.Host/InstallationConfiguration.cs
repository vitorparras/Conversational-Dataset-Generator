using System.Collections;
using Core.Platform;
using Core.Platform.Configuration;
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

    /// <summary>The installation the approved value declares.</summary>
    internal static Installation Of(IReadOnlyDictionary<string, string> values) =>
        new(string.Equals(values[KindPath], Demonstration, StringComparison.Ordinal) ? InstallationKind.Demonstration : InstallationKind.Real);

    /// <summary>The installation the configuration declares, and the report of versions that shows it beside the versions the checks confirmed.</summary>
    internal static void Add(IServiceCollection services, IReadOnlyDictionary<string, string> values, IEnumerable checks)
    {
        var installation = Of(values);
        _ = services.AddSingleton(installation).AddSingleton(StartupVersions.Of(checks).WithInstallation(installation));
    }

    /// <summary>Writes, once, the line that says which kind of installation this is.</summary>
    internal static void Announce(ILogger logger, IServiceProvider services) =>
        logger.Declared(services.GetRequiredService<Installation>());
}
