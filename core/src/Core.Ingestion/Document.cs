namespace Core.Ingestion;

/// <summary>Running text with no author for any part of it.</summary>
public sealed class Document : Material
{
    /// <summary>The document holds its sections; only the conference of the form makes one.</summary>
    /// <param name="sections">The texts of the document, in the order the content gives them.</param>
    internal Document(IReadOnlyList<string> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        Sections = sections;
    }

    /// <inheritdoc />
    public override MaterialForm Form => MaterialForm.Document;

    /// <summary>The texts of the document, in the order the content gives them.</summary>
    public IReadOnlyList<string> Sections { get; }

    /// <summary>Only how many sections there are.</summary>
    public override string ToString() => $"Document {{ Sections = {Sections.Count} }}";
}
