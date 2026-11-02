using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Platform.Keys;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Content;

/// <summary>
/// The rotation of the epoch (decision 0049): every key of a row not emptied that an older epoch wraps is wrapped again under the newest one, in one transaction that goes through
/// the domains one after the other; it commits all or none. Only the wrapped key and its epoch change, and never the content. The step that follows, the destruction of the old
/// epoch, is the custodian's and is not here.
/// </summary>
public sealed class EpochRotation
{
    private const int WrappedColumn = 2;
    private const string Domains = "SELECT id FROM public.domain ORDER BY id";
    private const string OlderItems =
        "SELECT id, dek_epoch, dek_wrapped FROM content.item_text WHERE emptied_at IS NULL AND dek_epoch < @newest ORDER BY id LIMIT 500";
    private const string OlderMaps =
        "SELECT id, dek_epoch, dek_wrapped FROM content.reversal_map WHERE emptied_at IS NULL AND dek_epoch < @newest ORDER BY id LIMIT 500";
    private const string ReplaceItem = "UPDATE content.item_text SET dek_epoch = @epoch, dek_wrapped = @wrapped WHERE id = @id AND emptied_at IS NULL";
    private const string ReplaceMap = "UPDATE content.reversal_map SET dek_epoch = @epoch, dek_wrapped = @wrapped WHERE id = @id AND emptied_at IS NULL";

    private static readonly Table Items = new(OlderItems, ReplaceItem);
    private static readonly Table Maps = new(OlderMaps, ReplaceMap);

    private readonly NpgsqlDataSource dataSource;
    private readonly ItemKeys keys;

    /// <summary>The rotation reads and writes the rows through the data source of the application's credential and wraps with <paramref name="keys"/>.</summary>
    public EpochRotation(NpgsqlDataSource dataSource, ItemKeys keys)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(keys);
        this.dataSource = dataSource;
        this.keys = keys;
    }

    /// <summary>Wraps again every key an older epoch wraps and says how many; nothing is changed if any key cannot be unwrapped, such as one whose epoch is not loaded.</summary>
    public async Task<int> RotateAsync(CancellationToken cancellationToken)
    {
        var domains = await ListDomainsAsync(cancellationToken).ConfigureAwait(false);
        if (domains.Count == 0)
        {
            return 0;
        }

        var transaction = await ContentTransaction.BeginAsync(dataSource, domains[0], cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var rewrapped = 0;
            var recorded = new Recorded();
            foreach (var domain in domains)
            {
                await transaction.ChangeDomainAsync(domain, cancellationToken).ConfigureAwait(false);
                rewrapped += await RewrapAsync(transaction, Items, recorded, cancellationToken).ConfigureAwait(false);
                rewrapped += await RewrapAsync(transaction, Maps, recorded, cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return rewrapped;
        }
    }

    private async Task<List<int>> ListDomainsAsync(CancellationToken cancellationToken)
    {
        var domains = new List<int>();
        var command = dataSource.CreateCommand(Domains);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    domains.Add(reader.GetInt32(0));
                }
            }
        }

        return domains;
    }

    private async Task<int> RewrapAsync(ContentTransaction transaction, Table table, Recorded recorded, CancellationToken cancellationToken)
    {
        var rewrapped = 0;
        while (true)
        {
            var older = await OlderAsync(transaction, table, cancellationToken).ConfigureAwait(false);
            if (older.Count == 0)
            {
                return rewrapped;
            }

            await RecordOnceAsync(transaction, recorded, cancellationToken).ConfigureAwait(false);
            foreach (var (id, epoch, wrapped) in older)
            {
                var again = keys.Rewrap(epoch, wrapped, keys.Newest);
                rewrapped += await ReplaceAsync(transaction, table, (id, again), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>The event of the rotation enters the chain before the first key is wrapped again, and once; the database refuses a key wrapped again without it (migration 0018).</summary>
    private async Task RecordOnceAsync(ContentTransaction transaction, Recorded recorded, CancellationToken cancellationToken)
    {
        if (recorded.Done)
        {
            return;
        }

        var result = await CustodyTransaction.AppendAsync(transaction.AsCustodyScope(), CustodyEvent.EpochRotated(keys.Newest), cancellationToken).ConfigureAwait(false);
        recorded.Done = result.Outcome == CustodyOutcome.Appended
            ? true
            : throw new InvalidOperationException($"The custody chain did not take the event of the rotation: {result.Outcome}.");
    }

    private async Task<List<(long Id, int Epoch, byte[] Wrapped)>> OlderAsync(ContentTransaction transaction, Table table, CancellationToken cancellationToken)
    {
        var older = new List<(long, int, byte[])>();
        var command = transaction.CreateCommand(table.Older);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("newest", NpgsqlDbType.Integer) { Value = keys.Newest });
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    older.Add((reader.GetInt64(0), reader.GetInt32(1), (byte[])reader.GetValue(WrappedColumn)));
                }
            }
        }

        return older;
    }

    private async Task<int> ReplaceAsync(ContentTransaction transaction, Table table, (long Id, byte[] Wrapped) row, CancellationToken cancellationToken)
    {
        var command = transaction.CreateCommand(table.Replace);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("epoch", NpgsqlDbType.Integer) { Value = keys.Newest });
            _ = command.Parameters.Add(new NpgsqlParameter("wrapped", NpgsqlDbType.Bytea) { Value = row.Wrapped });
            _ = command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = row.Id });
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record Table(string Older, string Replace);

    private sealed class Recorded
    {
        internal bool Done { get; set; }
    }
}
