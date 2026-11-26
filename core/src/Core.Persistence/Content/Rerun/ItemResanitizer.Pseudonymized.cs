using Core.Persistence.Content.Writing;
using Core.Persistence.Custody.Chain;
using NpgsqlTypes;
using static Core.Persistence.Content.Rerun.RerunRows;

namespace Core.Persistence.Content.Rerun;

/// <summary>
/// The rerun of a pseudonymized item (issue 102, F3): the domain swaps the new sanitized text again with the map the item holds, a marker new to the item is won for its batch in the same
/// transaction, drawn again without the ones another batch holds, and the old map is emptied and the new one written in its place.
/// </summary>
public sealed partial class ItemResanitizer
{
    /// <summary>The draws the rerun of a pseudonymized item has before it fails, each one without the markers the ones before it met in another batch.</summary>
    internal const int Draws = 8;

    /// <summary>
    /// What the domain makes of the item, drawn again for as many draws as <see cref="Draws"/> until its batch wins every marker new to the item, a marker held by no batch; the markers
    /// of its map need no draw. A text identical, or an item with no map, is not drawn.
    /// </summary>
    private static async Task<Resanitized?> DrawnAsync(
        CustodyScope scope, (RerunHold Hold, Func<RerunSource, Resanitized?> Rerun) work, RerunSource source, CancellationToken cancellationToken)
    {
        var produced = work.Rerun(source);
        if (produced?.Map is null || produced.Identical)
        {
            return produced;
        }

        var held = new HashSet<string>(source.Map?.Select(entry => entry.Marker) ?? [], StringComparer.Ordinal);
        var batch = await BatchOfAsync(scope, work.Hold.ItemId, cancellationToken).ConfigureAwait(false);
        var taken = new HashSet<string>(StringComparer.Ordinal);
        for (var draw = 1; ; draw++)
        {
            string[] fresh = [.. (produced.Map ?? []).Select(entry => entry.Marker).Where(marker => !held.Contains(marker)).Distinct(StringComparer.Ordinal)];
            held.UnionWith(await WonAsync(scope, (work.Hold.DomainId, batch), fresh, cancellationToken).ConfigureAwait(false));
            if (fresh.All(held.Contains))
            {
                return produced;
            }

            taken.UnionWith(fresh.Where(marker => !held.Contains(marker)));
            produced = draw < Draws && work.Rerun(source with { Taken = taken.ToHashSet(StringComparer.Ordinal) }) is { Map: not null } again
                ? again
                : throw new InvalidOperationException("Every draw of the rerun met a marker another batch holds.");
        }
    }

    /// <summary>The markers given that the batch won now, none of them held before by this batch or another.</summary>
    private static async Task<List<string>> WonAsync(CustodyScope scope, (int DomainId, long Batch) of, string[] markers, CancellationToken cancellationToken)
    {
        if (markers.Length == 0)
        {
            return [];
        }

        Bound[] values = [new("domain", NpgsqlDbType.Integer, of.DomainId), new("batch", NpgsqlDbType.Bigint, of.Batch), new("markers", TextArray, markers)];
        return await RowsAsync(scope, (ContentQueries.InsertMarkersWon, values), row => row.GetString(0), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The old map of the item emptied, with the event of its rerun, and the new one written for its batch, sealed under the key of the old one, which leaves with it.</summary>
    private async Task RemapAsync(CustodyScope scope, RerunHold hold, (IReadOnlyList<NewMapEntry> Entries, ContentKey Key) map, CancellationToken cancellationToken)
    {
        Bound[] emptied = [new("id", NpgsqlDbType.Bigint, hold.ItemId)];
        _ = await ScalarAsync(scope, (ContentQueries.EmptyReversalMaps, emptied), cancellationToken).ConfigureAwait(false);
        var entries = protectedContent.Reseal(ContentColumns.Entries, hold.DomainId, map.Key, MapEntryJson.Write(map.Entries)).Envelope.ToArray();
        Bound[] written =
        [
            new("domain", NpgsqlDbType.Integer, hold.DomainId), new("batch", NpgsqlDbType.Bigint, await BatchOfAsync(scope, hold.ItemId, cancellationToken).ConfigureAwait(false)),
            new("item", NpgsqlDbType.Bigint, hold.ItemId), new("entries", NpgsqlDbType.Bytea, entries), new("epoch", NpgsqlDbType.Integer, map.Key.Epoch),
            new("wrapped", NpgsqlDbType.Bytea, map.Key.Wrapped.ToArray()),
        ];
        _ = await ScalarAsync(scope, (ContentQueries.InsertReversalMap, written), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The batch of the item, whose conversation holds its markers.</summary>
    private static async Task<long> BatchOfAsync(CustodyScope scope, long itemId, CancellationToken cancellationToken)
    {
        Bound[] item = [new("item", NpgsqlDbType.Bigint, itemId)];
        return (long)(await ScalarAsync(scope, (ContentQueries.ReadItemBatch, item), cancellationToken).ConfigureAwait(false))!;
    }

    /// <summary>The markers of the map, each standing for an author when one of its entries does.</summary>
    private static List<HeldMarker> HeldOf(IEnumerable<NewMapEntry> map) =>
        [.. map.GroupBy(entry => entry.Marker, StringComparer.Ordinal).Select(same => new HeldMarker(same.Key, same.Any(entry => entry.Author)))];
}
