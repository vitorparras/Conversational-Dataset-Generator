using Core.Platform.Configuration;

namespace Core.Tests.Properties.Configuration;

/// <summary>A load result as text, so two results are compared whole and a difference reads as a diff.</summary>
internal static class LoadSnapshot
{
    public static string Of(ConfigurationLoadResult result) =>
        string.Join('\n', [$"refused={result.Refused}", .. ValueLines(result), .. ViewLines(result), .. ProblemLines(result)]);

    public static string ValuesOf(ConfigurationLoadResult result) => string.Join('\n', ValueLines(result));

    private static IEnumerable<string> ValueLines(ConfigurationLoadResult result) =>
        result.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"value {pair.Key}={pair.Value}");

    private static IEnumerable<string> ViewLines(ConfigurationLoadResult result) =>
        result.View.Entries.Select(entry => $"view {entry.Key}={entry.Value} from {entry.Origin}");

    private static IEnumerable<string> ProblemLines(ConfigurationLoadResult result) =>
        result.Problems.Select(problem => $"problem {problem.Kind} {problem.Key}: {problem.Describe()}");
}
