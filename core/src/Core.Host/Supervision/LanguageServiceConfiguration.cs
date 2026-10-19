using Core.Platform.Configuration;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host.Supervision;

/// <summary>The keys the supervision declares: which interpreter starts the service, and how long the service has to answer.</summary>
internal static class LanguageServiceConfiguration
{
    internal const string ExecutablePath = SectionName + ":" + ExecutableKey;

    internal const string StartupTimeoutPath = SectionName + ":" + StartupTimeoutKey;

    private const string SectionName = "LanguageService";
    private const string ExecutableKey = "Executable";
    private const string StartupTimeoutKey = "StartupTimeoutSeconds";

    internal static ConfigurationSection Section { get; } =
        new(
            SectionName,
            [
                ConfigurationKey.Required(ExecutableKey),
                ConfigurationKey.WithDefault(StartupTimeoutKey, "30"),
            ]);
}
