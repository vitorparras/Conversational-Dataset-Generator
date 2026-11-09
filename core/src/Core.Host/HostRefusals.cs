using Core.Host.Supervision;
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
        .. AdmissionConfiguration.Refusals(values),
        .. VerificationConfiguration.Refusals(values),
        .. SealConfiguration.Refusals(values),
        .. MailConfiguration.Refusals(values),
        .. TlsConfiguration.Refusals(values),
        .. AuditConfiguration.Refusals(values),
    ];
}
