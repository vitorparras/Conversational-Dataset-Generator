namespace Core.Ingestion;

/// <summary>The mark of a replicated message: it has the content of another and points to the first occurrence, which is marked too, pointing to itself. Nothing is removed or merged.</summary>
/// <param name="Of">The message that is marked.</param>
/// <param name="First">The first occurrence of its content.</param>
public sealed record ReplicationMark(ReplicationPlace Of, ReplicationPlace First);
