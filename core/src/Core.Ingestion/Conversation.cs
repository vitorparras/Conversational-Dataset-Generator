namespace Core.Ingestion;

/// <summary>A sequence of messages with authorship and time order; the messages are in the order of their moments, never of their place in the file.</summary>
public sealed class Conversation : Material
{
    /// <summary>The conversation holds the messages, by moment.</summary>
    /// <param name="messages">The messages, in the order of their moments.</param>
    public Conversation(IReadOnlyList<Message> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        Messages = messages;
    }

    /// <inheritdoc />
    public override MaterialForm Form => MaterialForm.Conversation;

    /// <summary>The messages, in the order of their moments.</summary>
    public IReadOnlyList<Message> Messages { get; }

    /// <summary>Only how many messages there are.</summary>
    public override string ToString() => $"Conversation {{ Messages = {Messages.Count} }}";
}
