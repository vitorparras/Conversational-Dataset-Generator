using System.Text;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Reads the records of a CSV text the way the server's csvlog writes it: fields separated by commas, a field in quotes when it holds a
/// comma, a quote or a line break, and a quote inside it doubled. One record at a time out of a buffer of its own, so that a log of any size is
/// read in constant memory and a call to the reader brings many characters and not one.
/// </summary>
internal sealed class CsvRecordReader
{
    internal const int DefaultBufferSize = 4096;

    private const char Quote = '"';
    private const char Separator = ',';

    private readonly TextReader reader;
    private readonly char[] buffer;
    private int position;
    private int length;

    internal CsvRecordReader(TextReader reader, int bufferSize = DefaultBufferSize)
    {
        this.reader = reader;
        buffer = new char[bufferSize];
    }

    /// <summary>The fields of the next record, or null at the end of the text. A record cut off inside quotes is the text as it stands, since the server may be writing it.</summary>
    internal async Task<IReadOnlyList<string>?> ReadAsync(CancellationToken cancellationToken)
    {
        var record = new Pending();
        var any = false;
        var ended = false;
        while (!ended && await NextAsync(cancellationToken).ConfigureAwait(false) is { } next)
        {
            any = true;
            ended = await TakeAsync(next, record, cancellationToken).ConfigureAwait(false);
        }

        record.Fields.Add(record.Field.ToString());
        return any ? record.Fields : null;
    }

    /// <summary>Takes one character into the record; true when it is the line break that ends it.</summary>
    private async Task<bool> TakeAsync(char next, Pending record, CancellationToken cancellationToken)
    {
        if (next == Quote)
        {
            record.InQuotes = await AfterQuoteAsync(record, cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (!record.InQuotes && next == '\n')
        {
            return true;
        }

        if (!record.InQuotes && next == Separator)
        {
            record.Fields.Add(record.Field.ToString());
            _ = record.Field.Clear();
        }
        else if (record.InQuotes || next != '\r')
        {
            _ = record.Field.Append(next);
        }
        else
        {
            // The carriage return before a line break that ends the record is no part of the last field.
        }

        return false;
    }

    /// <summary>Whether the text is inside quotes after a quote: one opens a field, one closes it, and a doubled one inside it is a quote of the text.</summary>
    private async Task<bool> AfterQuoteAsync(Pending record, CancellationToken cancellationToken)
    {
        if (!record.InQuotes)
        {
            return true;
        }

        if (await PeekAsync(cancellationToken).ConfigureAwait(false) != Quote)
        {
            return false;
        }

        position++;
        _ = record.Field.Append(Quote);
        return true;
    }

    private async ValueTask<char?> NextAsync(CancellationToken cancellationToken)
    {
        var next = await PeekAsync(cancellationToken).ConfigureAwait(false);
        if (next is not null)
        {
            position++;
        }

        return next;
    }

    private async ValueTask<char?> PeekAsync(CancellationToken cancellationToken)
    {
        if (position == length)
        {
            length = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            position = 0;
        }

        return position < length ? buffer[position] : null;
    }

    /// <summary>The record as far as it has been read: the fields closed, the one being read and whether the text is inside quotes.</summary>
    private sealed class Pending
    {
        internal List<string> Fields { get; } = [];

        internal StringBuilder Field { get; } = new();

        internal bool InQuotes { get; set; }
    }
}
