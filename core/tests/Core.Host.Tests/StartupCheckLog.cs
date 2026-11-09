using System.Text.RegularExpressions;

namespace Core.Host.Tests;

/// <summary>The lines the built host writes for each check of the startup (#632), read back: the name and the outcome of each, in the order the checks ran.</summary>
internal static partial class StartupCheckLog
{
    public static List<(string Name, string Outcome)> Of(string log) =>
        [.. Line().Matches(log).Select(line => (line.Groups["name"].Value, line.Groups["outcome"].Value))];

    public static string OutcomeOf(string log, string check) => Of(log).Single(entry => entry.Name == check).Outcome;

    [GeneratedRegex(@"Core\.Host\[342\] Conferência da subida (?<name>\S+): (?<outcome>\w+)")]
    private static partial Regex Line();
}
