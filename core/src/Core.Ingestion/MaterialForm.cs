namespace Core.Ingestion;

/// <summary>The four forms of material (decision 0005); a fifth asks for a new decision, never for a new value here.</summary>
public enum MaterialForm
{
    /// <summary>A sequence of messages, each with an author and a moment, whose order is that of the moments.</summary>
    Conversation = 0,

    /// <summary>One manifestation by one author, with no thread of conversation.</summary>
    Comment = 1,

    /// <summary>Running text with no author for any part of it.</summary>
    Document = 2,

    /// <summary>Text where the annotation falls on passages and not on the whole.</summary>
    LabeledSequence = 3,
}
