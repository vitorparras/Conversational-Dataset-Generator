using System.Globalization;
using Core.Boundary.Http;
using Core.Platform.Configuration;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host.Supervision;

/// <summary>The keys the supervision declares: which executable starts the service when it is not the folder's, how long it has to start, and how long one analysis may take.</summary>
internal static class LanguageServiceConfiguration
{
    internal const string ExecutablePath = SectionName + ":" + ExecutableKey;

    internal const string StartupTimeoutPath = SectionName + ":" + StartupTimeoutKey;

    internal const string AnalyzeTimeoutPath = SectionName + ":" + AnalyzeTimeoutKey;

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
                ConfigurationKey.WithDefault(AnalyzeTimeoutKey, "10"),
            ]);

    /// <summary>The executable the configuration gave; null when it did not, and the folder beside the executable then holds the interpreter.</summary>
    internal static string? GivenExecutable(IReadOnlyDictionary<string, string> values) =>
        values.GetValueOrDefault(ExecutablePath);

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
