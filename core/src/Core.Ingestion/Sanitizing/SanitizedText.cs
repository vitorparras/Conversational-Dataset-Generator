using Core.Boundary.Contracts;

namespace Core.Ingestion.Sanitizing;

/// <summary>
/// The text after every declared step, with the version and order of the list that produced it (decision 0015), the trail that translates its positions, what each step changed
/// and the pseudonymized occurrences where they now lie. Shown, it gives only the version and the count of each step, never a character of the text nor a position.
/// </summary>
public sealed record SanitizedText
{
    /// <summary>The sanitized text, with the typed marker in the place of what was removed.</summary>
    public string Text { get; internal init; } = string.Empty;

    /// <summary>The version of the list of steps that produced the text.</summary>
    public string Version { get; internal init; } = string.Empty;

    /// <summary>The steps that ran, in their order.</summary>
    public IReadOnlyList<StepIdentity> Order { get; internal init; } = [];

    /// <summary>How positions translate between the raw text and this one.</summary>
    public SanitizationTrail Trail { get; internal init; } = null!;

    /// <summary>What each step changed, in the order the steps ran, a step that changed nothing included.</summary>
    public IReadOnlyList<StepReport> Report { get; internal init; } = [];

    /// <summary>The pseudonymized occurrences, in the positions of the sanitized text, for the pseudonymization to replace.</summary>
    public IReadOnlyList<Occurrence> Pseudonyms { get; internal init; } = [];

    /// <summary>The version and the count of each step; never the text nor a position.</summary>
    public override string ToString() => $"SanitizedText {{ Version = {Version}, Steps = {string.Join(", ", Report.Select(step => $"{step.Name}: {step.Count}"))} }}";
}
