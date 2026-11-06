namespace Core.Ingestion;

/// <summary>
/// A labeled sequence needs a text and at least one passage in every unit, each passage inside its text and with some length. The limits are indexes of the text as the reader holds it,
/// so they are counted in its units; turning them into code points is the work of the converter of the boundary.
/// </summary>
internal sealed class LabeledSequenceRule : IFormRule
{
    /// <inheritdoc />
    public MaterialForm Form => MaterialForm.LabeledSequence;

    /// <inheritdoc />
    public ConferenceResult Confer(ReadMaterial read)
    {
        ArgumentNullException.ThrowIfNull(read);
        var missing = RuleChecks.Failing(read, MaterialField.Text, unit => RuleChecks.Blank(unit.Text));
        missing.AddRange(RuleChecks.Failing(read, MaterialField.Passages, unit => unit.Passages.Count == 0));
        var outside = RuleChecks.Failing(read, MaterialField.Passages, unit => unit.Passages.Any(passage => !Inside(passage, unit.Text)));
        return RuleChecks.Refusal(Form, RefusalCode.FieldMissing, missing)
            ?? RuleChecks.Refusal(Form, RefusalCode.PassageOutsideText, outside)
            ?? ConferenceResult.Accept(new LabeledSequence(RuleChecks.Each(read, unit => new LabeledText(unit.Text, unit.Passages))));
    }

    private static bool Inside(Passage passage, string text) => passage.Start >= 0 && passage.End > passage.Start && passage.End <= text.Length;
}
