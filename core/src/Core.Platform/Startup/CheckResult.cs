namespace Core.Platform.Startup;

/// <summary>The outcome of one check: which item, what was concluded, and where the item should come from.</summary>
public sealed record CheckResult(string Name, CheckStatus Status, string Message, string ExpectedSource)
{
    /// <summary>The item was found.</summary>
    public static CheckResult Passed(string name, string conclusion, string expectedSource) =>
        new(name, CheckStatus.Passed, conclusion, expectedSource);

    /// <summary>The item is missing; the conclusion says what and the source says where it should come from.</summary>
    public static CheckResult Failed(string name, string conclusion, string expectedSource) =>
        new(name, CheckStatus.Failed, conclusion, expectedSource);
}
