namespace Core.Platform.Startup;

/// <summary>The outcome of one startup check.</summary>
public enum CheckStatus
{
    /// <summary>The item is present and usable.</summary>
    Passed = 0,

    /// <summary>The item is missing or unusable; the process does not start.</summary>
    Failed = 1,
}

/// <summary>What one startup check found.</summary>
/// <param name="Name">The check, named the same way in the log and in the status.</param>
/// <param name="Status">Whether the item is there.</param>
/// <param name="Message">What was found, in words the operator reads.</param>
/// <param name="ExpectedSource">Where the item should come from.</param>
public sealed record CheckResult(string Name, CheckStatus Status, string Message, string ExpectedSource)
{
    /// <summary>A check that found its item.</summary>
    public static CheckResult Pass(string name, string message, string expectedSource) =>
        new(name, CheckStatus.Passed, message, expectedSource);

    /// <summary>A check that did not find its item.</summary>
    public static CheckResult Fail(string name, string message, string expectedSource) =>
        new(name, CheckStatus.Failed, message, expectedSource);
}
