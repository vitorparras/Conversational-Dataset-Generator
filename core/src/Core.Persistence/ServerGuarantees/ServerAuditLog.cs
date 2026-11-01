using System.Globalization;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// The audit lines the server wrote, read from the folder where its csvlog is. It only reads: it opens each file for reading while the server
/// may still be writing it, and never writes, renames or removes anything there.
/// </summary>
public sealed class ServerAuditLog(string directory)
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

    /// <summary>
    /// The lines written from <paramref name="from"/> (inclusive) to <paramref name="to"/> (exclusive), oldest first.
    /// A line that cannot be read as the server writes it is an error, not a line left out.
    /// </summary>
    public async Task<IReadOnlyList<ServerAuditEntry>> ReadAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var entries = new List<ServerAuditEntry>();
        foreach (var file in LogFilesSince(from))
        {
            entries.AddRange(await ReadFileAsync(file, from, to, cancellationToken).ConfigureAwait(false));
        }

        return entries;
    }

    private static async Task<List<ServerAuditEntry>> ReadFileAsync(FileSystemInfo file, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var entries = new List<ServerAuditEntry>();
        using var stream = new FileStream(
            file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var text = new StreamReader(stream);
        var records = new CsvRecordReader(text);
        while (await records.ReadAsync(cancellationToken).ConfigureAwait(false) is { } record)
        {
            if (await EntryAsync(record, file.Name, cancellationToken).ConfigureAwait(false) is { } entry && entry.At >= from && entry.At < to)
            {
                entries.Add(entry);
            }
        }

        return entries;
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
        var fields = await new CsvRecordReader(text).ReadAsync(cancellationToken).ConfigureAwait(false);
        return fields is { Count: >= PartsOfAnAudit }
            ? new ServerAuditEntry(at, record[RoleField], record[DatabaseField], fields[ClassPart], fields[CommandPart], Named(fields[ObjectTypePart]), Named(fields[ObjectNamePart]))
            : throw new InvalidDataException($"A linha de auditoria de {file} não tem as seis partes que o pgaudit escreve.");
    }

    private static string? Named(string field) => field.Length == 0 ? null : field;
}
