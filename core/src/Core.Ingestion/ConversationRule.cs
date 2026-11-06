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
        var notAllowed = RuleChecks.Failing(read, MaterialField.Passages, unit => unit.Passages.Count > 0);
        notAllowed.AddRange(Dangling(read));
        return RuleChecks.Refusal(Form, RefusalCode.FieldMissing, missing)
            ?? RuleChecks.Refusal(Form, RefusalCode.FieldNotAllowed, notAllowed)
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
        var ordered = read.Units.Select((unit, position) => (unit, position)).OrderBy(pair => pair.unit.Instant).ToList();
        var placed = new int[ordered.Count];
        for (var at = 0; at < ordered.Count; at++)
        {
            placed[ordered[at].position] = at;
        }

        var messages = new List<Message>();
        foreach (var (unit, _) in ordered)
        {
            messages.Add(new Message(unit.Author!, unit.Instant!.Value, unit.RepliesTo is { } to ? placed[to] : null, unit.Text));
        }

        return messages;
    }
}
