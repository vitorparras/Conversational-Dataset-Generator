using Core.Host.Supervision;

namespace Core.Host.Settings;

/// <summary>
/// The keys the screen may change (decision 0053): a list that names each one, so that a key the code learns is born outside it. Secrets, the custody of the key,
/// the certificate, the address the host listens on and the database are never here; they change only in the file, on the server.
/// </summary>
internal static class ChangeableKeys
{
    /// <summary>The keys, in the order the screen offers them.</summary>
    internal static IReadOnlyList<string> All { get; } = new[]
    {
        SessionConfiguration.IdleTimeoutPath,
        SessionConfiguration.AbsoluteTimeoutPath,
        PasswordConfiguration.WorkFactorPath,
        VerificationConfiguration.VerificationIntervalPath,
        AuditConfiguration.CompareIntervalPath,
        LanguageServiceConfiguration.StartupTimeoutPath,
        LanguageServiceConfiguration.AnalyzeTimeoutPath,
    }.AsReadOnly();

    /// <summary>Whether the screen may change the key, compared as the loader names it.</summary>
    internal static bool Has(string key) => All.Contains(key, StringComparer.Ordinal);
}
