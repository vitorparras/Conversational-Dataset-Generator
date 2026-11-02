using System.Globalization;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// The audit lines the server wrote, read from the folder where its csvlog is. It only reads: it opens each file for reading while the server
/// may still be writing it, and never writes, renames or removes anything there.
/// </summary>
public sealed class ServerAuditLog
{
    private const string AuditPrefix = "AUDIT: SESSION,";
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss.fff 'UTC'";
    private const int RoleField = 1;
    private const int DatabaseField = 2;
    private const int MessageField = 13;
    private const int FieldsOfALine = 14;
    private const int PartsOfAnAudit = 6;
    private const int ClassPart = 2;
    private const int CommandPart = 3;
    private const int ObjectTypePart = 4;
    private const int ObjectNamePart = 5;
    private const int NestedBufferSize = 256;
    private const int FileBufferSize = 4096;

    /// <summary>
    /// The lines of a file come in the order the server wrote them, which is by the time they were stamped to within a moment; a line this much later
    /// than the end of the interval is the sign that the rest of the log is later than the interval.
    /// </summary>
    private static readonly TimeSpan Slack = TimeSpan.FromMinutes(1);

    private readonly string directory;

    /// <summary>The log is read from the folder where the server writes its csvlog.</summary>
    public ServerAuditLog(string directory)
    {
        this.directory = directory;
    }

    /// <summary>
    /// The lines written from <paramref name="from"/> (inclusive) to <paramref name="to"/> (exclusive), oldest first.
    /// A line that cannot be read as the server writes it is an error, not a line left out.
    /// </summary>
    public Task<IReadOnlyList<ServerAuditEntry>> ReadAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        ReadAsync(from, to, int.MaxValue, cancellationToken);

    /// <summary>
    /// The same lines, at most <paramref name="limit"/> of them: the reading stops there, so that an interval wider than the caller means to take
    /// costs the memory of the limit and not of the log. It also stops at the first line later than the interval, and opens no file after it.
    /// </summary>
    public async Task<IReadOnlyList<ServerAuditEntry>> ReadAsync(DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken cancellationToken)
    {
        var window = new Window(from, to, limit);
        var done = false;
        using var files = LogFilesSince(from).GetEnumerator();
        while (!done && files.MoveNext())
        {
            done = await ReadFileAsync(files.Current, window, cancellationToken).ConfigureAwait(false);
        }

        return window.Entries;
    }

    /// <summary>Reads the file into the entries; true when nothing after it is needed, because the limit is reached or the lines are past the interval.</summary>
    private static async Task<bool> ReadFileAsync(FileSystemInfo file, Window window, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(
            file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var text = new StreamReader(stream);
        var records = new CsvRecordReader(text);
        while (await records.ReadAsync(cancellationToken).ConfigureAwait(false) is { } record)
        {
            if (await EntryAsync(record, file.Name, cancellationToken).ConfigureAwait(false) is not { } entry)
            {
                continue;
            }

            if (entry.At >= window.To + Slack)
            {
                return true;
            }

            if (entry.At >= window.From && entry.At < window.To)
            {
                window.Entries.Add(entry);
            }

            if (window.Entries.Count >= window.Limit)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The csv files of the folder in the order the server wrote them, without the ones that stopped being written before the interval began.</summary>
    private IEnumerable<FileInfo> LogFilesSince(DateTimeOffset from) =>
        new DirectoryInfo(directory).EnumerateFiles("*.csv")
            .Where(file => file.LastWriteTimeUtc >= from.UtcDateTime)
            .OrderBy(file => file.Name, StringComparer.Ordinal);

    private static async Task<ServerAuditEntry?> EntryAsync(IReadOnlyList<string> record, string file, CancellationToken cancellationToken)
    {
        if (record.Count < FieldsOfALine || !record[MessageField].StartsWith(AuditPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        if (!DateTimeOffset.TryParseExact(record[0], TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at))
        {
            throw new InvalidDataException($"A linha de auditoria de {file} tem hora que não está em UTC no formato do servidor.");
        }

        var audit = record[MessageField][AuditPrefix.Length..];
        using var text = new StringReader(audit);
        var fields = await new CsvRecordReader(text, NestedBufferSize).ReadAsync(cancellationToken).ConfigureAwait(false);
        return fields is { Count: >= PartsOfAnAudit }
            ? new ServerAuditEntry(at, record[RoleField], record[DatabaseField], fields[ClassPart], fields[CommandPart], Named(fields[ObjectTypePart]), Named(fields[ObjectNamePart]))
            : throw new InvalidDataException($"A linha de auditoria de {file} não tem as seis partes que o pgaudit escreve.");
    }

    private static string? Named(string field) => field.Length == 0 ? null : field;

    /// <summary>The interval asked, the most lines wanted and the lines found so far.</summary>
    private sealed record Window(DateTimeOffset From, DateTimeOffset To, int Limit)
    {
        internal List<ServerAuditEntry> Entries { get; } = [];
    }
}
