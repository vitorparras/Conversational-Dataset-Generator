namespace Core.Ingestion.Sanitizing;

/// <summary>
/// How a position on the raw text becomes a position on the sanitized one, and back, composed from the edits each step returned and from nothing else. Positions count code
/// points. What touched a removed span no longer exists, and a position outside the text is a defect, never a guess (decision 0012, which reverses the pseudonym through them).
/// </summary>
public sealed class SanitizationTrail
{
    private readonly IReadOnlyList<TrailStep> steps;

    internal SanitizationTrail(int rawLength, IReadOnlyList<TrailStep> steps)
    {
        RawLength = rawLength;
        this.steps = steps;
        SanitizedLength = steps.Count == 0 ? rawLength : steps[^1].OutputLength;
        Steps = [.. steps.Select(step => step.Edits)];
    }

    /// <summary>How many code points the raw text has.</summary>
    public int RawLength { get; }

    /// <summary>How many code points the sanitized text has.</summary>
    public int SanitizedLength { get; }

    /// <summary>The edits each step returned, in the order the steps ran, each in the positions of the text that step received.</summary>
    public IReadOnlyList<IReadOnlyList<SanitizationEdit>> Steps { get; }

    /// <summary>The trail rebuilt from what each step recorded, refused unless the edits of each step fit the text it received.</summary>
    /// <param name="rawLength">How many code points the raw text has.</param>
    /// <param name="steps">What each step changed, in the order the steps ran.</param>
    public static SanitizationTrail Of(int rawLength, IReadOnlyList<StepReport> steps)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rawLength);
        ArgumentNullException.ThrowIfNull(steps);
        var built = new List<TrailStep>(steps.Count);
        var length = rawLength;
        foreach (var report in steps)
        {
            var step = TrailStep.Of(length, report?.Edits ?? throw new ArgumentException("A step is missing.", nameof(steps)));
            built.Add(step);
            length = step.OutputLength;
        }

        return new SanitizationTrail(rawLength, built);
    }

    /// <summary>Where a span of the raw text lies in the sanitized one.</summary>
    /// <param name="start">The code point offset where the span starts in the raw text.</param>
    /// <param name="end">The code point offset just past the span in the raw text.</param>
    public TrailSpan Forward(int start, int end)
    {
        Inside(start, end, RawLength);
        var span = TrailSpan.At(start, end);
        for (var index = 0; index < steps.Count && span.Exists; index++)
        {
            span = steps[index].Forward(span.Start, span.End);
        }

        return span;
    }

    /// <summary>Where a span of the sanitized text lies in the raw one.</summary>
    /// <param name="start">The code point offset where the span starts in the sanitized text.</param>
    /// <param name="end">The code point offset just past the span in the sanitized text.</param>
    public TrailSpan Back(int start, int end)
    {
        Inside(start, end, SanitizedLength);
        var span = TrailSpan.At(start, end);
        for (var index = steps.Count - 1; index >= 0 && span.Exists; index--)
        {
            span = steps[index].Back(span.Start, span.End);
        }

        return span;
    }

    /// <summary>The edit count of each step; never the offsets.</summary>
    public override string ToString() => $"SanitizationTrail {{ Edits = {string.Join(", ", Steps.Select(edits => edits.Count))} }}";

    private static void Inside(int start, int end, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfLessThan(end, start);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(end, length);
    }
}
