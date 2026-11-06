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
}
