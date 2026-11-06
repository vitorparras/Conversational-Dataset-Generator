namespace Core.Ingestion;

/// <summary>
/// A conversation needs an author and a moment for every message, and the moments tell the order. Two messages with the same moment are ordered by the answer of one to the other,
/// near or far, and when nothing in the content decides, or the answers contradict the moments or each other, the batch is refused: the place in the file never orders.
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
        var notAllowed = RuleChecks.Failing(read, MaterialField.Passages, unit => unit.Passages.Count > 0);
        notAllowed.AddRange(Dangling(read));
        return RuleChecks.Refusal(Form, RefusalCode.FieldMissing, missing)
            ?? RuleChecks.Refusal(Form, RefusalCode.FieldNotAllowed, notAllowed)
            ?? RuleChecks.Refusal(Form, RefusalCode.OrderIncoherent, ConversationOrder.Incoherent(read))
            ?? RuleChecks.Refusal(Form, RefusalCode.OrderNotDetermined, ConversationOrder.Undecided(read))
            ?? ConferenceResult.Accept(new Conversation(InOrder(read)));
    }

    /// <summary>The answers that name no unit of the material, or the unit itself.</summary>
    private static List<Finding> Dangling(ReadMaterial read)
    {
        var dangling = new List<Finding>();
        for (var position = 0; position < read.Units.Count; position++)
        {
            if (read.Units[position].RepliesTo is { } to && (to < 0 || to >= read.Units.Count || to == position))
            {
                dangling.Add(new Finding(position, MaterialField.RepliesTo));
            }
        }

        return dangling;
    }

    /// <summary>The messages in the order of the moments, each answer pointing at the message answered where it now stands.</summary>
    private static List<Message> InOrder(ReadMaterial read)
    {
        var ordered = ConversationOrder.Sequence(read);
        var placed = new int[ordered.Count];
        for (var at = 0; at < ordered.Count; at++)
        {
            placed[ordered[at]] = at;
        }

        var messages = new List<Message>();
        foreach (var position in ordered)
        {
            var unit = read.Units[position];
            messages.Add(new Message(unit.Author!, unit.Instant!.Value, unit.RepliesTo is { } to ? placed[to] : null, unit.Text, messages.Count));
        }

        return messages;
    }
}
