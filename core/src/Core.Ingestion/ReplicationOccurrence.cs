namespace Core.Ingestion;

/// <summary>
/// A message as the criterion sees it: where it is, the keyed digest of its text and of its sender, how long the text is and when it was said. Nothing in it is the text or the name of the sender,
/// and the digests are not undone without the key, so that what the criterion compares is never in clear.
/// </summary>
/// <param name="Place">Where the message is kept.</param>
/// <param name="Text">The keyed digest of the text.</param>
/// <param name="Sender">The keyed digest of the sender.</param>
/// <param name="Length">The length of the text, in characters.</param>
/// <param name="Instant">When the message was said.</param>
public sealed record ReplicationOccurrence(ReplicationPlace Place, string Text, string Sender, int Length, DateTimeOffset Instant);
