using Core.Persistence.Content.Sealing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain.Append;
using Core.Persistence.Domains;
using Core.Platform.Keys;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Content.Rotation;

/// <summary>
/// The rewrap of a domain (issue 56): the key of each row not emptied is wrapped again under the newest generation of its labels that is registered and not revoked, in batches that each commit
/// alone with the event that ties the change to the chain (decision 0050). Only the wrapped key changes, never the content or the epoch; a run that stops leaves its batches, and the next goes on.
/// </summary>
public sealed class DomainRewrap
{
    private const int WholeBatch = 500;
    private static readonly Table Items = new(ContentQueries.ReadItemKeysAfter, ContentQueries.RewrapItemKey);
    private static readonly Table Maps = new(ContentQueries.ReadMapKeysAfter, ContentQueries.RewrapMapKey);

    private readonly NpgsqlDataSource dataSource;
    private readonly int batch;

    /// <summary>The rewrap reads and writes through the data source of the owner credential, in batches of <paramref name="batch"/> rows (a test makes them small to stop a run between two).</summary>
    public DomainRewrap(NpgsqlDataSource dataSource, int batch = WholeBatch)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batch);
        this.dataSource = dataSource;
        this.batch = batch;
    }

    /// <summary>Wraps again the keys of the domain that are behind, and says how many; a key that cannot be opened stops the run, and the batches already committed stay.</summary>
    public async Task<int> RewrapAsync(int domainId, ItemKeys keys, DerivationLabels loaded, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var plan = await PlanAsync(domainId, loaded, cancellationToken).ConfigureAwait(false);
        var items = await RewrapTableAsync(plan, keys, Items, cancellationToken).ConfigureAwait(false);
        return items + await RewrapTableAsync(plan, keys, Maps, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>How many keys of the rows not emptied are under the newest generation not revoked, and how many are behind; it only reads.</summary>
    public async Task<(int Current, int Behind)> StatusAsync(int domainId, DerivationLabels loaded, CancellationToken cancellationToken)
    {
        var plan = await PlanAsync(domainId, loaded, cancellationToken).ConfigureAwait(false);
        var items = await CountTableAsync(plan, Items, cancellationToken).ConfigureAwait(false);
        var maps = await CountTableAsync(plan, Maps, cancellationToken).ConfigureAwait(false);
        return (items.Current + maps.Current, items.Behind + maps.Behind);
    }

    private async Task<Plan> PlanAsync(int domainId, DerivationLabels loaded, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(domainId);
        ArgumentNullException.ThrowIfNull(loaded);
        var registered = await RegisteredLabels.ReadAsync(dataSource, cancellationToken).ConfigureAwait(false);
        var revoked = await LabelRevocations.ReadAsync(dataSource, cancellationToken).ConfigureAwait(false);
        return new Plan { DomainId = domainId, Loaded = loaded, Usable = text => registered.Contains(text) && !revoked.Contains(text) };
    }

    private async Task<int> RewrapTableAsync(Plan plan, ItemKeys keys, Table table, CancellationToken cancellationToken)
    {
        var rewrapped = 0;
        long after = 0;
        while (true)
        {
            var (last, read, changed) = await RewrapBatchAsync(plan, keys, (table, after), cancellationToken).ConfigureAwait(false);
            rewrapped += changed;
            if (read < batch)
            {
                return rewrapped;
            }

            after = last;
        }
    }

    private async Task<(long Last, int Read, int Changed)> RewrapBatchAsync(Plan plan, ItemKeys keys, (Table Table, long After) page, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, plan.DomainId, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var rows = await ReadAsync(transaction, page, cancellationToken).ConfigureAwait(false);
            var changed = await ChangeAsync(transaction, (plan, keys), (page.Table, rows), cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return (rows.Count == 0 ? page.After : rows[^1].Id, rows.Count, changed);
        }
    }

    private static async Task<int> ChangeAsync(
        ContentTransaction transaction, (Plan Plan, ItemKeys Keys) context, (Table Table, List<(long Id, int Epoch, byte[] Wrapped)> Rows) page, CancellationToken cancellationToken)
    {
        var behind = page.Rows.Where(row => context.Plan.IsBehind(row.Wrapped)).ToList();
        if (behind.Count == 0)
        {
            return 0;
        }

        var result = await CustodyAppend.AppendAsync(transaction.AsCustodyScope(), CustodyEvent.DomainRewrapped(context.Plan.DomainId), cancellationToken).ConfigureAwait(false);
        _ = result.Outcome == CustodyOutcome.Appended ? 0 : throw new InvalidOperationException($"The custody chain did not take the event of the rewrap: {result.Outcome}.");
        foreach (var (id, epoch, wrapped) in behind)
        {
            var again = context.Keys.Rewrap(epoch, wrapped, epoch, context.Plan.Target(wrapped));
            _ = await ReplaceAsync(transaction, page.Table, (id, epoch, again), cancellationToken).ConfigureAwait(false);
        }

        return behind.Count;
    }

    private async Task<(int Current, int Behind)> CountTableAsync(Plan plan, Table table, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, plan.DomainId, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var (current, behind, after) = (0, 0, 0L);
            while (true)
            {
                var rows = await ReadAsync(transaction, (table, after), cancellationToken).ConfigureAwait(false);
                var late = rows.Count(row => plan.IsBehind(row.Wrapped));
                (current, behind) = (current + rows.Count - late, behind + late);
                if (rows.Count < batch)
                {
                    return (current, behind);
                }

                after = rows[^1].Id;
            }
        }
    }

    private async Task<List<(long Id, int Epoch, byte[] Wrapped)>> ReadAsync(ContentTransaction transaction, (Table Table, long After) page, CancellationToken cancellationToken)
    {
        var command = transaction.CreateCommand(page.Table.Read);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("after", NpgsqlDbType.Bigint) { Value = page.After });
            _ = command.Parameters.Add(new NpgsqlParameter("batch", NpgsqlDbType.Integer) { Value = batch });
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await EpochRotation.CollectOlderAsync(reader, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<int> ReplaceAsync(ContentTransaction transaction, Table table, (long Id, int Epoch, byte[] Wrapped) row, CancellationToken cancellationToken)
    {
        var command = transaction.CreateCommand(table.Replace);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("epoch", NpgsqlDbType.Integer) { Value = row.Epoch });
            _ = command.Parameters.Add(new NpgsqlParameter("wrapped", NpgsqlDbType.Bytea) { Value = row.Wrapped });
            _ = command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = row.Id });
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record Table(string Read, string Replace);

    private sealed class Plan
    {
        internal int DomainId { get; init; }

        internal DerivationLabels Loaded { get; init; } = DerivationLabels.None;

        internal Func<string, bool> Usable { get; init; } = static _ => false;

        internal bool IsBehind(byte[] wrapped) => EnvelopeCipher.LabelOf(wrapped) != Target(wrapped);

        internal string Target(byte[] wrapped)
        {
            var carried = DerivationLabel.TryParse(EnvelopeCipher.LabelOf(wrapped), out var label) && label.DomainId == DomainId
                ? label
                : throw new EnvelopeRefusedException("The label in the header of a key is not a derivation label of the domain.");
            return ProtectedContent.NewestOf(Loaded, Usable, carried.Purpose, DomainId).Text;
        }
    }
}
