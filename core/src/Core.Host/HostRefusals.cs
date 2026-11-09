using Core.Host.Settings;
using Core.Host.Supervision;
using Core.Persistence.Connection;
using Core.Platform.Configuration;

namespace Core.Host;

/// <summary>What the values the host reads refuse, apart from the shape of the keys.</summary>
internal static class HostRefusals
{
    /// <summary>What the values of the language service, of the safekeeping declaration, of the work factor, of the addresses, of the conditions and of the audit folder refuse, together.</summary>
    internal static IReadOnlyList<ConfigurationProblem> Of(IReadOnlyDictionary<string, string> values, TimeSpan? analyzeTimeout) =>
    [
        .. LanguageServiceConfiguration.Refusals(values, analyzeTimeout),
        .. SafekeepingConfiguration.Refusals(values, InstallationConfiguration.Of(values).Kind),
        .. PasswordConfiguration.Refusals(values),
        .. SessionConfiguration.Refusals(values),
        .. ChangeDeadlineConfiguration.Refusals(values),
        .. AdmissionConfiguration.Refusals(values),
        .. VerificationConfiguration.Refusals(values),
        .. SealConfiguration.Refusals(values),
        .. TlsConfiguration.Refusals(values),
        .. AuditConfiguration.Refusals(values),
    ];

    /// <summary>
    /// What a configuration loaded for the service refuses, in the order the start judges it: the shape of the keys, then the database, then the values together.
    /// The start and the trial of a change of configuration both ask it, so that the trial refuses exactly what the next start would.
    /// </summary>
    internal static IReadOnlyList<ConfigurationProblem> OfService(ConfigurationLoadResult loaded) =>
        HostConfiguration.DatabaseOf(loaded) switch
        {
            null => loaded.Problems,
            { State: DatabaseSettingsState.Refused } database => database.Problems,
            _ => Of(loaded.Values, LanguageServiceConfiguration.AnalyzeTimeout(loaded.Values)),
        };
}
