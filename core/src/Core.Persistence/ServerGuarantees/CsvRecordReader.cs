using System.Text;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Reads the records of a CSV text the way the server's csvlog writes it: fields separated by commas, a field in quotes when it holds a
/// comma, a quote or a line break, and a quote inside it doubled. One record at a time, so that a log of any size is read in constant memory.
/// </summary>
internal sealed class CsvRecordReader(TextReader reader)
{
    private const char Quote = '"';
    private const char Separator = ',';

    /// <summary>The fields of the next record, or null at the end of the text. A record cut off inside quotes is the text as it stands, since the server may be writing it.</summary>
    internal async Task<IReadOnlyList<string>?> ReadAsync(CancellationToken cancellationToken)
    {
        var record = new Pending();
        var any = false;
        var ended = false;
        var buffer = new char[1];
        while (!ended && await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false) == 1)
        {
            any = true;
            ended = Take(buffer[0], record);
        }

        record.Fields.Add(record.Field.ToString());
        return any ? record.Fields : null;
    }

    /// <summary>Takes one character into the record; true when it is the line break that ends it.</summary>
    private bool Take(char next, Pending record)
    {
        if (next == Quote)
        {
            record.InQuotes = AfterQuote(record.InQuotes, record.Field);
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
    private bool AfterQuote(bool wasInQuotes, StringBuilder field)
    {
        if (!wasInQuotes)
        {
            return true;
        }

        if (reader.Peek() != Quote)
        {
            return false;
        }

        _ = reader.Read();
        _ = field.Append(Quote);
        return true;
    }

    /// <summary>The record as far as it has been read: the fields closed, the one being read and whether the text is inside quotes.</summary>
    private sealed class Pending
    {
        internal List<string> Fields { get; } = [];

        internal StringBuilder Field { get; } = new();

        internal bool InQuotes { get; set; }
    }
}
