using System.Text.RegularExpressions;

namespace Core.Tests.Properties.Harness;

/// <summary>What a failing sample says: the seed that replays it and the counterexample it settled on.</summary>
internal static partial class FailedSample
{
    public static string SeedIn(string message) => SeedPattern().Match(message).Groups["seed"].Value;

    /// <summary>The line after the header, where the shrunk input is printed.</summary>
    public static string CounterexampleIn(string message) => message.Split('\n')[1].Trim();

    [GeneratedRegex(@"CsCheck_Seed=(?<seed>\S+)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SeedPattern();
}
