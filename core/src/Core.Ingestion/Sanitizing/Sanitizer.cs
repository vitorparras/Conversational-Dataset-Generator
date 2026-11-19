using System.Text;
using Core.Boundary.Contracts;
using Core.Ingestion.Deciding;

namespace Core.Ingestion.Sanitizing;

/// <summary>
/// Runs the declared steps over a text whose occurrences were decided, in their order, each over the text the step before it left, and records the version of the list in what it
/// returns (decision 0015). A step returns edits and never the text; the sanitizer applies them, writing the typed marker in the place of what was removed, and carries every
/// occurrence that survived to the positions of the new text, so that the pseudonymized can be resolved and reversed through the trail (decision 0012). A material whose
/// destinations were not decided is refused, since nothing then says what may stay.
/// </summary>
public sealed class Sanitizer(SanitizationSteps steps, ITextBoundaries boundaries)
{
    private readonly SanitizationSteps steps = steps ?? throw new ArgumentNullException(nameof(steps));
    private readonly ITextBoundaries boundaries = boundaries ?? throw new ArgumentNullException(nameof(boundaries));

    /// <summary>The sanitized text, its trail and the report of each step, or the refusal when no destination was decided.</summary>
    /// <param name="raw">The text as it came, whose positions count code points.</param>
    /// <param name="decided">Every occurrence found in the text, with its destination; null when the destinations were not decided.</param>
    /// <exception cref="ArgumentException">An occurrence falls outside the text, or a step returned edits that do not fit the text it received.</exception>
    public SanitizationResult Sanitize(string raw, IReadOnlyList<DecidedOccurrence>? decided)
    {
        ArgumentNullException.ThrowIfNull(raw);
        if (decided is null)
        {
            return SanitizationResult.Refused(SanitizationRefusal.Undecided);
        }

        var rawLength = boundaries.CodePointCount(raw);
        IReadOnlyList<DecidedOccurrence> occurrences = Checked(decided, rawLength);
        var text = raw;
        var length = rawLength;
        var trail = new List<TrailStep>(steps.Steps.Count);
        var report = new List<StepReport>(steps.Steps.Count);
        foreach (var step in steps.Steps)
        {
            var applied = TrailStep.Of(length, step.Edits(new StepInput(text, occurrences)));
            text = Apply(text, applied);
            occurrences = Carried(occurrences, applied);
            length = applied.OutputLength;
            trail.Add(applied);
            report.Add(new StepReport(step.Name, step.Version, applied.Edits));
        }

        var pseudonyms = occurrences.Where(occurrence => occurrence.Destination == Destination.Pseudonymize).Select(occurrence => occurrence.Occurrence).ToList();
        var sanitized = new SanitizedText
        {
            Text = text,
            Version = steps.Version,
            Order = steps.Order,
            Trail = new SanitizationTrail(rawLength, trail),
            Report = report,
            Pseudonyms = pseudonyms,
        };
        return SanitizationResult.Of(sanitized);
    }

    private static List<DecidedOccurrence> Checked(IReadOnlyList<DecidedOccurrence> decided, int length)
    {
        foreach (var occurrence in decided)
        {
            var span = occurrence?.Occurrence ?? throw new ArgumentException("An occurrence is missing.", nameof(decided));
            if (span.Start < 0 || span.End < span.Start || span.End > length)
            {
                throw new ArgumentException("An occurrence falls outside the text.", nameof(decided));
            }
        }

        return [.. decided];
    }

    private static List<DecidedOccurrence> Carried(IReadOnlyCollection<DecidedOccurrence> occurrences, TrailStep step)
    {
        var carried = new List<DecidedOccurrence>(occurrences.Count);
        foreach (var decided in occurrences)
        {
            var span = step.Forward(decided.Occurrence.Start, decided.Occurrence.End);
            if (span.Exists)
            {
                carried.Add(decided with { Occurrence = decided.Occurrence with { Start = span.Start, End = span.End } });
            }
        }

        return carried;
    }

    private string Apply(string text, TrailStep step)
    {
        var builder = new StringBuilder(text.Length);
        var at = 0;
        foreach (var edit in step.Edits)
        {
            _ = builder.Append(boundaries.Span(text, at, edit.Start)).Append(SanitizationMarkers.Text(edit.Marker));
            at = edit.End;
        }

        return builder.Append(boundaries.Span(text, at, step.InputLength)).ToString();
    }
}
