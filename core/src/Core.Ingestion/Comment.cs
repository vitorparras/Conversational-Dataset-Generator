namespace Core.Ingestion;

/// <summary>An isolated manifestation, with an author and with no thread of conversation.</summary>
public sealed class Comment : Material
{
    /// <summary>The comment of an author; only the conference of the form makes one.</summary>
    /// <param name="author">Who wrote it.</param>
    /// <param name="instant">The moment the content gives for it; null when it gives none.</param>
    /// <param name="text">The text of the comment.</param>
    internal Comment(string author, DateTimeOffset? instant, string text)
    {
        ArgumentNullException.ThrowIfNull(author);
        ArgumentNullException.ThrowIfNull(text);
        Author = author;
        Instant = instant;
        Text = text;
    }

    /// <inheritdoc />
    public override MaterialForm Form => MaterialForm.Comment;

    /// <summary>Who wrote it.</summary>
    public string Author { get; }

    /// <summary>The moment the content gives for it; null when it gives none.</summary>
    public DateTimeOffset? Instant { get; }

    /// <summary>The text of the comment.</summary>
    public string Text { get; }

    /// <summary>Only whether there is a moment: neither the author nor the text is told.</summary>
    public override string ToString() => $"Comment {{ Instant = {Instant is not null} }}";
}
