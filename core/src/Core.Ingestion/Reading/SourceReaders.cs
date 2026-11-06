namespace Core.Ingestion.Reading;

/// <summary>The readers the installation has, one for each format: a format with no reader is unreadable, and no reader is tried by approximation.</summary>
public sealed class SourceReaders
{
    private readonly Dictionary<string, ISourceReader> byFormat = new(StringComparer.Ordinal);

    /// <summary>The registry holds the readers given; two readers for one format is a defect.</summary>
    /// <param name="readers">The readers, one for each format.</param>
    public SourceReaders(IEnumerable<ISourceReader> readers)
    {
        ArgumentNullException.ThrowIfNull(readers);
        foreach (var reader in readers)
        {
            ArgumentNullException.ThrowIfNull(reader);
            if (!byFormat.TryAdd(reader.Format, reader))
            {
                throw new ArgumentException("There are two readers for the same format.", nameof(readers));
            }
        }
    }

    /// <summary>The registry with the readers of the first version.</summary>
    public static SourceReaders Default() => new(Enumerable.Repeat<ISourceReader>(new ExportedConversationReader(), 1));

    /// <summary>Reads the content with the reader of the format named; a format with no reader is unreadable as an unsupported format.</summary>
    /// <param name="format">The format the content was submitted as.</param>
    /// <param name="content">The bytes of the content.</param>
    public ReadOutcome Read(string format, ReadOnlyMemory<byte> content)
    {
        ArgumentNullException.ThrowIfNull(format);
        return byFormat.TryGetValue(format, out var reader) ? reader.Read(content) : ReadOutcome.Unreadable(UnreadableReason.UnsupportedFormat);
    }
}
