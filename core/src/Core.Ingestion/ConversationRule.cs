namespace Core.Ingestion;

/// <summary>
/// A conversation needs an author and a moment for every message, and the moments tell the order; two messages with the same moment have no order but the one of the file,
/// so the batch is refused.
/// </summary>
internal sealed class ConversationRule : IFormRule
{
    /// <inheritdoc />
    public MaterialForm Form => MaterialForm.Conversation;

    /// <inheritdoc />
    public ConferenceResult Confer(ReadMaterial read)
    {
        ArgumentNullException.ThrowIfNull(read);
        var missing = RuleChecks.Failing(read, MaterialField.Text, unit => RuleChecks.Blank(unit.Text));
        missing.AddRange(RuleChecks.Failing(read, MaterialField.Author, unit => RuleChecks.Blank(unit.Author)));
        missing.AddRange(RuleChecks.Failing(read, MaterialField.Instant, unit => unit.Instant is null));
        var passages = RuleChecks.Failing(read, MaterialField.Passages, unit => unit.Passages.Count > 0);
        return RuleChecks.Refusal(Form, RefusalCode.FieldMissing, missing)
            ?? RuleChecks.Refusal(Form, RefusalCode.FieldNotAllowed, passages)
            ?? RuleChecks.Refusal(Form, RefusalCode.OrderNotDetermined, Tied(read))
            ?? ConferenceResult.Accept(new Conversation(InOrder(read)));
    }

    private static List<Finding> Tied(ReadMaterial read)
    {
        var tied = new List<Finding>();
        foreach (var same in read.Units.Select((unit, position) => (unit.Instant, position)).GroupBy(pair => pair.Instant).Where(group => group.Count() > 1))
        {
            foreach (var (_, position) in same)
            {
                tied.Add(new Finding(position, MaterialField.Instant));
            }
        }

        tied.Sort((left, right) => Nullable.Compare(left.Unit, right.Unit));
        return tied;
    }

    private static List<Message> InOrder(ReadMaterial read)
    {
        var messages = new List<Message>();
        foreach (var unit in read.Units.OrderBy(unit => unit.Instant))
        {
            messages.Add(new Message(unit.Author!, unit.Instant!.Value, unit.RepliesTo, unit.Text));
        }

        return messages;
    }
}
