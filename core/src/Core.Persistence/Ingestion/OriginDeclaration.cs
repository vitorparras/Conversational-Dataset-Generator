using Core.Persistence.Content;
using Core.Persistence.Content.Sealing;

namespace Core.Persistence.Ingestion;

/// <summary>
/// Where the material of a submission came from, as the submission declared it: a text that is sealed, as the raw text of an item is, or the explicit choice of saying it is unknown.
/// The text never leaves this type in clear: what the batch keeps in clear is only <see cref="State" />, and the text is read back only by the reader of protected content, which records the reading.
/// </summary>
public sealed record OriginDeclaration
{
    /// <summary>The state of a batch whose submission declared an origin.</summary>
    public const string DeclaredState = "declared";

    /// <summary>The state of a batch whose submission chose to say the origin is unknown.</summary>
    public const string UnknownState = "unknown";

    /// <summary>The most characters the text of an origin may have, counted before it is sealed.</summary>
    public const int MostCharacters = 500;

    private OriginDeclaration(SealedContent? content, ContentKey? key)
    {
        Content = content;
        Key = key;
    }

    /// <summary>The submission that chose to say the origin is unknown; nothing is sealed for it.</summary>
    public static OriginDeclaration Unknown { get; } = new(null, null);

    /// <summary>The state kept in clear with the batch: declared or unknown, never the text.</summary>
    public string State => Content is null ? UnknownState : DeclaredState;

    internal SealedContent? Content { get; }

    internal ContentKey? Key { get; }

    /// <summary>Seals the text of an origin for the domain, under a key drawn for it; the text must have something in it and no more than <see cref="MostCharacters" /> characters.</summary>
    /// <param name="sealing">The path that seals protected content.</param>
    /// <param name="domainId">The domain of the batch.</param>
    /// <param name="text">What the submission said about where the material came from.</param>
    public static OriginDeclaration Declared(ProtectedContent sealing, int domainId, string text)
    {
        ArgumentNullException.ThrowIfNull(sealing);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, MostCharacters);
        using var row = sealing.Begin(ContentColumns.OriginKey, domainId);
        return new OriginDeclaration(row.Seal(ContentColumns.Origin, text), row.Stored);
    }
}
