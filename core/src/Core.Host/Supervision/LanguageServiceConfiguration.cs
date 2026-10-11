using System.Globalization;
using Core.Platform.Configuration;

namespace Core.Host.Supervision;

/// <summary>How the core finds and starts the language service.</summary>
/// <param name="Command">The executable to start; it is looked up as the system looks up any command.</param>
/// <param name="Arguments">The arguments that come before <c>--port</c>, which the core appends.</param>
/// <param name="StartupTimeout">How long the service has to answer its first authenticated request.</param>
internal sealed record LanguageServiceOptions(string Command, IReadOnlyList<string> Arguments, TimeSpan StartupTimeout)
{
    /// <summary>The separator of <see cref="LanguageServiceConfiguration.Arguments"/>, as in the bundled files key.</summary>
    public const char ArgumentSeparator = ';';

    /// <summary>Reads the options from the configuration in force.</summary>
    public static LanguageServiceOptions FromConfiguration(EffectiveConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new LanguageServiceOptions(
            configuration.Get(LanguageServiceConfiguration.Command),
            [.. configuration.Get(LanguageServiceConfiguration.Arguments)
                .Split(ArgumentSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
            TimeSpan.FromSeconds(int.Parse(
                configuration.Get(LanguageServiceConfiguration.StartupTimeoutSeconds),
                NumberStyles.None,
                CultureInfo.InvariantCulture)));
    }
}

/// <summary>The keys the supervision declares.</summary>
internal static class LanguageServiceConfiguration
{
    public const string Command = "LanguageService:Command";
    public const string Arguments = "LanguageService:Arguments";
    public const string StartupTimeoutSeconds = "LanguageService:StartupTimeoutSeconds";

    /// <summary>
    /// The service is what stops unanalysed text from passing as clean, so the command that starts
    /// it is a protection: required, with no default to fall back on.
    /// </summary>
    public static ConfigurationSection Section { get; } = new(
        "LanguageService",
        [
            ConfigurationKey.Protection("Command"),
            ConfigurationKey.WithDefault("Arguments", string.Empty),
            ConfigurationKey.WithDefault("StartupTimeoutSeconds", "30", ValueFormat.PositiveInteger),
        ]);
}
