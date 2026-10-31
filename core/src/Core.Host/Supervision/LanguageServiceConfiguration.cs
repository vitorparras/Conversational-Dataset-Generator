using System.Globalization;
using Core.Boundary.Http;
using Core.Platform.Configuration;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host.Supervision;

/// <summary>The keys the supervision declares: which executable starts the service when it is not the folder's (development only), the start time and the time one analysis may take.</summary>
internal static class LanguageServiceConfiguration
{
    internal const string ExecutablePath = SectionName + ":" + ExecutableKey;

    internal const string StartupTimeoutPath = SectionName + ":" + StartupTimeoutKey;

    internal const string AnalyzeTimeoutPath = SectionName + ":" + AnalyzeTimeoutKey;

    /// <summary>The variable of the process that names the environment the host runs in, the one .NET itself reads.</summary>
    internal const string EnvironmentVariable = "DOTNET_ENVIRONMENT";

    private const string DevelopmentEnvironment = "Development";
    private const string SectionName = "LanguageService";
    private const string ExecutableKey = "Executable";
    private const string StartupTimeoutKey = "StartupTimeoutSeconds";
    private const string AnalyzeTimeoutKey = "AnalyzeTimeoutSeconds";

    private static readonly int LongestAnalyzeSeconds = (int)HttpRecognizerSettings.LongestTimeout.TotalSeconds;
    private static readonly string AcceptedSeconds = $"número inteiro de segundos de 1 a {LongestAnalyzeSeconds}";

    internal static ConfigurationSection Section { get; } =
        new(
            SectionName,
            [
                ConfigurationKey.Optional(ExecutableKey),
                ConfigurationKey.WithDefault(StartupTimeoutKey, "30"),
                ConfigurationKey.WithDefault(AnalyzeTimeoutKey, "10")
            ]);

    /// <summary>Whether the environment named is the development one; any other name, or none, is not.</summary>
    internal static bool IsDevelopment(string? environment) =>
        string.Equals(environment, DevelopmentEnvironment, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether the process runs in the development environment.</summary>
    internal static bool InDevelopment() => IsDevelopment(Environment.GetEnvironmentVariable(EnvironmentVariable));

    /// <summary>The executable the configuration gave when the process runs in the development environment; null otherwise.</summary>
    internal static string? GivenExecutable(IReadOnlyDictionary<string, string> values) => GivenExecutable(values, InDevelopment());

    /// <summary>The executable the configuration gave; null when it did not or the environment is not the development one, and the folder is then checked.</summary>
    internal static string? GivenExecutable(IReadOnlyDictionary<string, string> values, bool development) =>
        development ? values.GetValueOrDefault(ExecutablePath) : null;

    /// <summary>The refusal of the executable key written outside the development environment; null when it is not written there.</summary>
    internal static ConfigurationProblem? ExecutableRefused(IReadOnlyDictionary<string, string> values, bool development) =>
        development || !values.ContainsKey(ExecutablePath)
            ? null
            : new ConfigurationProblem(
                ConfigurationProblemKind.ValueNotAccepted,
                ExecutablePath,
                values[ExecutablePath],
                [$"nenhum fora do ambiente {DevelopmentEnvironment} ({EnvironmentVariable}={DevelopmentEnvironment})"]);

    /// <summary>Every reason the supervision keys are refused: the executable written outside development, then an analysis time limit out of range.</summary>
    internal static IReadOnlyList<ConfigurationProblem> Refusals(IReadOnlyDictionary<string, string> values, TimeSpan? analyzeTimeout) =>
        [.. new[] { ExecutableRefused(values, InDevelopment()), analyzeTimeout is null ? AnalyzeTimeoutRefused(values) : null }.OfType<ConfigurationProblem>()];

    /// <summary>The time one analysis may take; null when the value is not a whole number of seconds from one to the longest the recognizer accepts.</summary>
    internal static TimeSpan? AnalyzeTimeout(IReadOnlyDictionary<string, string> values) =>
        int.TryParse(values[AnalyzeTimeoutPath], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
        && seconds > 0
        && seconds <= LongestAnalyzeSeconds
            ? TimeSpan.FromSeconds(seconds)
            : null;

    /// <summary>The refusal of an analysis time limit outside the accepted range.</summary>
    internal static ConfigurationProblem AnalyzeTimeoutRefused(IReadOnlyDictionary<string, string> values) =>
        new(ConfigurationProblemKind.ValueNotAccepted, AnalyzeTimeoutPath, values[AnalyzeTimeoutPath], [AcceptedSeconds]);
}
