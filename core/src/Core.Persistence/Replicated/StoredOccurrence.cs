namespace Core.Persistence.Replicated;

/// <summary>A message as the database keeps it for the replicated content: where it is, the keyed digests of its text and sender, the length of the text and when it was said.</summary>
/// <param name="Item">The item that holds the message.</param>
/// <param name="Position">The position of the message in its item, from zero.</param>
/// <param name="TextDigest">The keyed digest of the text: 64 lowercase hexadecimal characters.</param>
/// <param name="SenderDigest">The keyed digest of the sender: 64 lowercase hexadecimal characters.</param>
/// <param name="Length">The length of the text, in characters.</param>
/// <param name="SaidAt">When the message was said.</param>
public sealed record StoredOccurrence(long Item, int Position, string TextDigest, string SenderDigest, int Length, DateTimeOffset SaidAt);
