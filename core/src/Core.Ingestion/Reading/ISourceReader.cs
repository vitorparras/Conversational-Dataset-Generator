namespace Core.Ingestion.Reading;

/// <summary>
/// The reader of one source format: it takes the bytes that already went through the boundary and gives back what it read, whole, or the reason it could not.
/// It does not check the form, store, encrypt or know a file name, and it has no state, no disk, no network and no clock.
/// </summary>
public interface ISourceReader
{
    /// <summary>The format this reader reads, as the content declares it.</summary>
    string Format { get; }

    /// <summary>Reads the content whole; a content that cannot be read whole is unreadable, and nothing of it is handed over.</summary>
    /// <param name="content">The bytes of the content.</param>
    ReadOutcome Read(ReadOnlyMemory<byte> content);

    /// <summary>What the reader will read and no more; past it the material is refused with a reason.</summary>
    ReadLimits Limits { get; }

    /// <summary>Whether the first bytes of a content, at most <see cref="ReadLimits.HeadBytes" /> of them, are of the format of this reader; the name of the file is never asked.</summary>
    /// <param name="head">The first bytes of the content.</param>
    bool Recognizes(ReadOnlySpan<byte> head);
}
