using Core.Persistence.Content.Sealing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain.Append;
using Core.Persistence.Domains;
using Core.Platform.Keys;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Content.Rotation;

/// <summary>The rewrap of a domain (issue 56): each key not emptied is wrapped again under the newest registered label not revoked, in batches that each commit with their event (0050).</summary>
public sealed class DomainRewrap
{
    private const string DomainExists = "SELECT EXISTS (SELECT 1 FROM public.domain WHERE id = @id)";
    private static readonly Table Items = new(ContentQueries.ReadItemKeysAfter, ContentQueries.RewrapItemKey);
    private static readonly Table Maps = new(ContentQueries.ReadMapKeysAfter, ContentQueries.RewrapMapKey);

    private readonly NpgsqlDataSource dataSource;
    private readonly int batch;

    /// <summary>The rewrap reads and writes through the data source of the owner credential, in batches of <paramref name="batch"/> rows (a test makes them small to stop a run between two).</summary>
    public DomainRewrap(NpgsqlDataSource dataSource, int batch)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batch);
        this.dataSource = dataSource;
        this.batch = batch;
    }

    /// <summary>Wraps again the keys of the domain that are behind, and says how many; a key that cannot be opened stops the run, and the batches already committed stay.</summary>
    public Task<int> RewrapAsync(int domainId, ItemKeys keys, DerivationLabels loaded, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(domainId);
        ArgumentNullException.ThrowIfNull(loaded);
        return RewrapPlannedAsync(domainId, keys, loaded, cancellationToken);
    }

    /// <summary>How many keys of the rows not emptied are under the newest generation not revoked, and how many are behind; it only reads.</summary>
    public Task<RewrapStatus> StatusAsync(int domainId, DerivationLabels loaded, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(domainId);
        ArgumentNullException.ThrowIfNull(loaded);
        return StatusPlannedAsync(domainId, loaded, cancellationToken);
    }

    private async Task<int> RewrapPlannedAsync(int domainId, ItemKeys keys, DerivationLabels loaded, CancellationToken cancellationToken)
    {
        var plan = await PlanAsync(domainId, loaded, cancellationToken).ConfigureAwait(false);
        var items = await RewrapTableAsync(plan, keys, Items, cancellationToken).ConfigureAwait(false);
        return items + await RewrapTableAsync(plan, keys, Maps, cancellationToken).ConfigureAwait(false);
    }

    private async Task<RewrapStatus> StatusPlannedAsync(int domainId, DerivationLabels loaded, CancellationToken cancellationToken)
    {
        var plan = await PlanAsync(domainId, loaded, cancellationToken).ConfigureAwait(false);
        var (itemsCurrent, itemsBehind) = await CountTableAsync(plan, Items, cancellationToken).ConfigureAwait(false);
        var (mapsCurrent, mapsBehind) = await CountTableAsync(plan, Maps, cancellationToken).ConfigureAwait(false);
        return new RewrapStatus(itemsCurrent + mapsCurrent, itemsBehind + mapsBehind);
    }

    private async Task<Plan> PlanAsync(int domainId, DerivationLabels loaded, CancellationToken cancellationToken)
    {
        await EnsureDomainAsync(domainId, cancellationToken).ConfigureAwait(false);
        var registered = await RegisteredLabels.ReadAsync(dataSource, cancellationToken).ConfigureAwait(false);
        var revoked = await LabelRevocations.ReadAsync(dataSource, cancellationToken).ConfigureAwait(false);
        return new Plan(domainId, loaded, DerivationLabels.Of(registered), text => !revoked.Contains(text));
    }

    private async Task EnsureDomainAsync(int domainId, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(DomainExists);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Integer) { Value = domainId });
            if (!(bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!)
            {
                throw new InvalidOperationException($"The domain {domainId} does not exist.");
            }
        }
    }

    private async Task<int> RewrapTableAsync(Plan plan, ItemKeys keys, Table table, CancellationToken cancellationToken)
    {
        var (rewrapped, after) = (0, 0L);
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
        var changed = 0;
        foreach (var (id, epoch, wrapped) in behind)
        {
            var again = context.Keys.Rewrap(epoch, wrapped, epoch, context.Plan.Target(wrapped));
            changed += await ReplaceAsync(transaction, page.Table, (id, epoch, again), cancellationToken).ConfigureAwait(false);
        }

        return changed;
    }

    private async Task<(int Current, int Behind)> CountTableAsync(Plan plan, Table table, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, plan.DomainId, cancellationToken).ConfigureAwait(false);
        await using var scope = transaction.ConfigureAwait(false);
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

    private sealed record Plan(int DomainId, DerivationLabels Loaded, DerivationLabels Wanted, Func<string, bool> Usable)
    {
        internal bool IsBehind(byte[] wrapped) => EnvelopeCipher.LabelOf(wrapped) != Target(wrapped);

        internal string Target(byte[] wrapped)
        {
            var carried = DerivationLabel.TryParse(EnvelopeCipher.LabelOf(wrapped), out var label) && label.DomainId == DomainId
                ? label
                : throw new EnvelopeRefusedException("The label in the header of a key is not a derivation label of the domain.");
            var wanted = ProtectedContent.NewestOf(Wanted, Usable, carried.Purpose, DomainId).Text;
            return Loaded.Holds(wanted) ? wanted : throw new EnvelopeRefusedException($"The label {wanted} is the newest registered and not revoked, and the file of labels does not hold it.");
        }
    }
}
