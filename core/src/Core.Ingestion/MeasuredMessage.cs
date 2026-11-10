namespace Core.Ingestion;

/// <summary>A message with the length of its text already counted, in code points, by the converter: the criterion never counts them itself.</summary>
/// <param name="Message">The message of a conversation.</param>
/// <param name="Length">The length of its text in code points.</param>
public sealed record MeasuredMessage(Message Message, int Length);
