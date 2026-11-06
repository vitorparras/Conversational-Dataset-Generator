namespace Core.Ingestion;

/// <summary>
/// One unit of what a reader read: its text and what the content itself carried with it. There is no file name and no place on the page,
/// so that neither the order nor the authorship can come from them.
/// </summary>
/// <param name="Text">The text of the unit.</param>
/// <param name="Author">Who wrote it, when the content says.</param>
/// <param name="Instant">The moment the content gives for it.</param>
/// <param name="RepliesTo">The author it answers, when the content says.</param>
/// <param name="Passages">The limits of the passages the content marks in the text; none when it marks none.</param>
public sealed record ReadUnit(string Text, string? Author, DateTimeOffset? Instant, string? RepliesTo, IReadOnlyList<Passage> Passages)
{
    /// <summary>The text of the unit.</summary>
    public string Text { get; } = Text ?? throw new ArgumentNullException(nameof(Text));

    /// <summary>The limits of the passages marked in the text; empty when none is marked.</summary>
    public IReadOnlyList<Passage> Passages { get; } = Passages ?? throw new ArgumentNullException(nameof(Passages));

    /// <summary>Never the text, the author or the author answered: the unit tells only what it carries.</summary>
    public override string ToString() => $"ReadUnit {{ Author = {Author is not null}, Instant = {Instant is not null}, RepliesTo = {RepliesTo is not null}, Passages = {Passages.Count} }}";
}
