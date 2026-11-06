namespace Core.Ingestion;

/// <summary>One part of a conversation: who wrote it and where it stands in the order, when it was written, the message it answers when the content says, and its text.</summary>
/// <param name="Author">Who wrote the message.</param>
/// <param name="Instant">The moment the content gives for the message, which orders the conversation.</param>
/// <param name="RepliesTo">The position, among the messages of the conversation in its order, of the message answered; null when the content does not say.</param>
/// <param name="Text">The text of the message.</param>
/// <param name="Rank">The rank of the message in the conversation, from zero: the place the moments and the answers give it, never the place in the file.</param>
public sealed record Message(string Author, DateTimeOffset Instant, int? RepliesTo, string Text, int Rank)
{
    /// <summary>Only the moment: neither the author nor the text of the message is told.</summary>
    public override string ToString() => $"Message {{ Instant = {Instant:O} }}";
}
