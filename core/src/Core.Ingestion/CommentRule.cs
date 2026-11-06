namespace Core.Ingestion;

/// <summary>A comment is one unit, with an author and a text, and with no one it answers: a comment that answers is a message of a thread.</summary>
internal sealed class CommentRule : IFormRule
{
    /// <inheritdoc />
    public MaterialForm Form => MaterialForm.Comment;

    /// <inheritdoc />
    public ConferenceResult Confer(ReadMaterial read)
    {
        ArgumentNullException.ThrowIfNull(read);
        var extra = new List<Finding>();
        for (var position = 1; position < read.Units.Count; position++)
        {
            extra.Add(new Finding(position, null));
        }

        var missing = RuleChecks.Failing(read, MaterialField.Text, unit => RuleChecks.Blank(unit.Text));
        missing.AddRange(RuleChecks.Failing(read, MaterialField.Author, unit => RuleChecks.Blank(unit.Author)));
        var notAllowed = RuleChecks.Failing(read, MaterialField.RepliesTo, unit => unit.RepliesTo is not null);
        notAllowed.AddRange(RuleChecks.Failing(read, MaterialField.Passages, unit => unit.Passages.Count > 0));
        var only = read.Units[0];
        return RuleChecks.Refusal(Form, RefusalCode.MoreThanOneUnit, extra)
            ?? RuleChecks.Refusal(Form, RefusalCode.FieldMissing, missing)
            ?? RuleChecks.Refusal(Form, RefusalCode.FieldNotAllowed, notAllowed)
            ?? ConferenceResult.Accept(new Comment(only.Author!, only.Instant, only.Text));
    }
}
