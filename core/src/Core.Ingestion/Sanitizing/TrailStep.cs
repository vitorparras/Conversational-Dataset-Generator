namespace Core.Ingestion.Sanitizing;

/// <summary>
/// The edits of one step, checked against the text the step received, with where each one lies before and after it. A span that touches an edit, wholly or in part, no longer
/// exists; any other moves by what the edits before it added or took away.
/// </summary>
internal sealed class TrailStep
{
    private readonly Segment[] forward;
    private readonly Segment[] back;

    private TrailStep(IReadOnlyList<SanitizationEdit> edits, int inputLength, Segment[] forward)
    {
        Edits = edits;
        InputLength = inputLength;
        this.forward = forward;
        back = [.. forward.Select(Reversed)];
        OutputLength = inputLength + (forward.Length == 0 ? 0 : forward[^1].ToEnd - forward[^1].FromEnd);
    }

    internal IReadOnlyList<SanitizationEdit> Edits { get; }

    internal int InputLength { get; }

    internal int OutputLength { get; }

    /// <summary>The edits of a step, refused unless they are ordered, disjoint, not empty, inside the text the step received and write either a marker or a text.</summary>
    internal static TrailStep Of(int inputLength, IReadOnlyList<SanitizationEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        var segments = new Segment[edits.Count];
        var previousEnd = 0;
        var shift = 0;
        for (var index = 0; index < edits.Count; index++)
        {
            var edit = edits[index] ?? throw new ArgumentException("An edit is missing.", nameof(edits));
            if (edit.Start < previousEnd || edit.End <= edit.Start || edit.End > inputLength || !edit.IsWellFormed)
            {
                throw new ArgumentException("The edits must be ordered, disjoint, not empty, inside the text and write a marker or a text.", nameof(edits));
            }

            var to = edit.Start + shift;
            segments[index] = new Segment(edit.Start, edit.End, to, to + edit.Width);
            shift = segments[index].ToEnd - edit.End;
            previousEnd = edit.End;
        }

        return new TrailStep([.. edits], inputLength, segments);
    }

    /// <summary>The span of the text the step received, in the text it left.</summary>
    internal TrailSpan Forward(int start, int end) => Map(forward, start, end);

    /// <summary>The span of the text the step left, in the text it received.</summary>
    internal TrailSpan Back(int start, int end) => Map(back, start, end);

    private static TrailSpan Map(Segment[] segments, int start, int end)
    {
        var shift = 0;
        foreach (var segment in segments)
        {
            if (start < segment.FromEnd && segment.FromStart < end)
            {
                return TrailSpan.Gone;
            }

            if (segment.FromEnd <= start)
            {
                shift = segment.ToEnd - segment.FromEnd;
            }
        }

        return TrailSpan.At(start + shift, end + shift);
    }

    private static Segment Reversed(Segment segment)
    {
        var (receivedStart, receivedEnd, leftStart, leftEnd) = segment;
        return new Segment(leftStart, leftEnd, receivedStart, receivedEnd);
    }

    private readonly record struct Segment(int FromStart, int FromEnd, int ToStart, int ToEnd);
}
