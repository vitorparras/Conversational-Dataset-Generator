using Core.Boundary.Contracts;

namespace Core.Ingestion;

/// <summary>
/// A labeled sequence needs a text and at least one passage in every unit, each passage inside its text and with some length, and no limit between the two halves of a surrogate pair.
/// The limits are indexes of the text as the reader holds it; the pair is told by the boundary, the only place that tells code points.
/// </summary>
internal sealed class LabeledSequenceRule : IFormRule
{
    /// <inheritdoc />
    public MaterialForm Form => MaterialForm.LabeledSequence;

    /// <inheritdoc />
    public ConferenceResult Confer(ReadMaterial read, ITextBoundaries boundaries)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(boundaries);
        var missing = RuleChecks.Failing(read, MaterialField.Text, unit => RuleChecks.Blank(unit.Text));
        missing.AddRange(RuleChecks.Failing(read, MaterialField.Passages, unit => unit.Passages.Count == 0));
        var outside = RuleChecks.Failing(read, MaterialField.Passages, unit => unit.Passages.Any(passage => !Inside(passage, unit.Text)));
        var splits = RuleChecks.Failing(read, MaterialField.Passages, unit => unit.Passages.Any(passage => Splits(boundaries, passage, unit.Text)));
        return RuleChecks.Refusal(Form, RefusalCode.FieldMissing, missing)
            ?? RuleChecks.Refusal(Form, RefusalCode.PassageOutsideText, outside)
            ?? RuleChecks.Refusal(Form, RefusalCode.PassageSplitsPair, splits)
            ?? ConferenceResult.Accept(new LabeledSequence(RuleChecks.Each(read, unit => new LabeledText(unit.Text, unit.Passages))));
    }

    private static bool Inside(Passage passage, string text) => passage.Start >= 0 && passage.End > passage.Start && passage.End <= text.Length;

    private static bool Splits(ITextBoundaries boundaries, Passage passage, string text) =>
        Inside(passage, text) && (boundaries.SplitsPair(text, passage.Start) || boundaries.SplitsPair(text, passage.End));
}
