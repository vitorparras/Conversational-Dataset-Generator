using System.Security.Cryptography;
using Core.Persistence.Content;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Custody;
using Core.Platform.Keys;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// A small sample, the same every time: the first record of the raw text and of the map in the first domains that hold one, read as the system through the path that records the access
/// and opened under the label its envelope declares. Opening all would expose content for nothing.
/// </summary>
internal sealed class EncryptedSample
{
    private const int MostPassedOver = 16;

    private const string Domains = "SELECT coalesce(pg_catalog.array_agg(domain_id), ARRAY[]::integer[]) FROM (SELECT domain_id FROM public.content_domain "
        + "WHERE table_name = @table GROUP BY domain_id HAVING pg_catalog.sum(delta) > 0 ORDER BY domain_id LIMIT 3) first_domains";

    private readonly NpgsqlDataSource dataSource;
    private readonly Func<CancellationToken, Task<MasterKeyReading>> read;
    private readonly Func<MasterKeyReading, ProtectedContentReader> open;

    internal EncryptedSample(NpgsqlDataSource dataSource, Func<CancellationToken, Task<MasterKeyReading>> read, Func<MasterKeyReading, ProtectedContentReader> open)
    {
        this.dataSource = dataSource;
        this.read = read;
        this.open = open;
    }

    /// <summary>
    /// Whether every record of the sample opened: a key not read, a label not loaded, a label that is not the column's and an access the chain did not record all say no.
    /// A record under the scope of a declared loss is passed over for the next one of the domain, up to a limit; when all that is reachable is under the loss, or the limit is reached,
    /// the verdict says which and is not a no.
    /// </summary>
    internal async Task<SampleVerdict> VerdictAsync(CancellationToken cancellationToken)
    {
        using var reading = await read(cancellationToken).ConfigureAwait(false);
        if (reading.Failure is not null)
        {
            return SampleVerdict.NotOpened;
        }

        var reader = open(reading);
        SampleVerdict? verdict = null;
        foreach (var column in new[] { ContentColumns.RawText, ContentColumns.Entries })
        {
            foreach (var domain in await DomainsAsync(column.Table, cancellationToken).ConfigureAwait(false))
            {
                verdict = Combined(verdict, await OpenedAsync(reader, column, domain, cancellationToken).ConfigureAwait(false));
            }
        }

        return verdict ?? SampleVerdict.Opened;
    }

    private static SampleVerdict Combined(SampleVerdict? soFar, SampleVerdict next)
    {
        if (soFar is not { } earlier)
        {
            return next;
        }

        if (earlier == SampleVerdict.NotOpened || next == SampleVerdict.NotOpened)
        {
            return SampleVerdict.NotOpened;
        }

        if (earlier == SampleVerdict.Opened || next == SampleVerdict.Opened)
        {
            return SampleVerdict.Opened;
        }

        return earlier == SampleVerdict.SearchStopped ? earlier : next;
    }

    private static async Task<SampleVerdict> OpenedAsync(ProtectedContentReader reader, ProtectedColumn column, int domain, CancellationToken cancellationToken)
    {
        var after = 0L;
        for (var passedOver = 0; passedOver <= MostPassedOver; passedOver++)
        {
            var read = await ReadAfterAsync(reader, new ContentRead(CustodyAuthor.System, column, domain, after), cancellationToken).ConfigureAwait(false);
            if (Judged(read, passedOver) is { } verdict)
            {
                return verdict;
            }

            after = read!.RowId;
        }

        return SampleVerdict.SearchStopped;
    }

    // Null means the record is under the scope of a loss: look at the next one.
    private static SampleVerdict? Judged(ContentReadResult? read, int passedOver) => read?.Outcome switch
    {
        null => SampleVerdict.NotOpened,
        ContentReadOutcome.KeyLossDeclared => null,
        ContentReadOutcome.NotRecorded or ContentReadOutcome.LabelMissing => SampleVerdict.NotOpened,
        ContentReadOutcome.NotFound => passedOver > 0 ? SampleVerdict.NoneOutsideLoss : SampleVerdict.Opened,
        ContentReadOutcome.Read or ContentReadOutcome.Empty or ContentReadOutcome.Emptied => SampleVerdict.Opened,
        _ => throw new ArgumentOutOfRangeException(nameof(read)),
    };

    private static async Task<ContentReadResult?> ReadAfterAsync(ProtectedContentReader reader, ContentRead after, CancellationToken cancellationToken)
    {
        try
        {
            return await reader.ReadFirstAsync(after, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception refused) when (refused is EnvelopeRefusedException or CryptographicException)
        {
            return null;
        }
    }

    private async Task<int[]> DomainsAsync(string table, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(Domains);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.AddWithValue("table", NpgsqlDbType.Text, table);
            return (int[])(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        }
    }
}
