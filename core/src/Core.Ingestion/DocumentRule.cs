using Core.Boundary.Contracts;

namespace Core.Ingestion;

/// <summary>A document is running text with no author, no one answered and no passages for any part of it; authorship by part is refused, and never dropped.</summary>
internal sealed class DocumentRule : IFormRule
{
    /// <inheritdoc />
    public MaterialForm Form => MaterialForm.Document;

    /// <inheritdoc />
    public ConferenceResult Confer(ReadMaterial read, ITextBoundaries boundaries)
    {
        ArgumentNullException.ThrowIfNull(read);
        var missing = RuleChecks.Failing(read, MaterialField.Text, unit => RuleChecks.Blank(unit.Text));
        var byPart = RuleChecks.Failing(read, MaterialField.Author, unit => unit.Author is not null);
        var notAllowed = RuleChecks.Failing(read, MaterialField.RepliesTo, unit => unit.RepliesTo is not null);
        notAllowed.AddRange(RuleChecks.Failing(read, MaterialField.Passages, unit => unit.Passages.Count > 0));
        return RuleChecks.Refusal(Form, RefusalCode.FieldMissing, missing)
            ?? RuleChecks.Refusal(Form, RefusalCode.AuthorshipByPart, byPart)
            ?? RuleChecks.Refusal(Form, RefusalCode.FieldNotAllowed, notAllowed)
            ?? ConferenceResult.Accept(new Document(RuleChecks.Each(read, unit => unit.Text)));
    }
}
