using Core.Persistence.Content.Sealing;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody.Chain;
using NpgsqlTypes;
using static Core.Persistence.Content.Rerun.RerunRows;

namespace Core.Persistence.Content.Rerun;

/// <summary>
/// The markers of a pseudonymized item rerun (issues 101 and 102): the domain swaps the new sanitized text again with the map the item holds, a marker new to the item is won for its batch in
/// the same transaction, drawn again without the ones another batch holds, the ones it held are kept, the ones gone from its text leave it, held still, and the old map is emptied and the
/// new one written in its place.
/// </summary>
internal static class RerunMarkers
{
    /// <summary>The draws the rerun of a pseudonymized item has before it fails, each one without the markers the ones before it met in another batch.</summary>
    internal const int Draws = 8;

    private const NpgsqlDbType TextArray = (NpgsqlDbType)((int)NpgsqlDbType.Array + (int)NpgsqlDbType.Text);
    private const NpgsqlDbType BooleanArray = (NpgsqlDbType)((int)NpgsqlDbType.Array + (int)NpgsqlDbType.Boolean);

    /// <summary>
    /// What the domain makes of the item, drawn again for as many draws as <see cref="Draws"/> until its batch wins every marker new to the item, a marker held by no batch; the markers
    /// of its map need no draw. A text identical, or an item with no map, is not drawn.
    /// </summary>
    internal static async Task<Resanitized?> DrawnAsync(
        CustodyScope scope, (RerunHold Hold, Func<RerunSource, Resanitized?> Rerun) work, RerunSource source, CancellationToken cancellationToken)
    {
        var produced = work.Rerun(source);
        if (produced?.Map is null || produced.Identical)
        {
            return produced;
        }

        var held = MarkersOf(source.Map);
        var batch = await BatchOfAsync(scope, work.Hold.ItemId, cancellationToken).ConfigureAwait(false);
        var taken = new HashSet<string>(StringComparer.Ordinal);
        var draw = 1;
        while (await LostAsync(scope, (work.Hold.DomainId, batch), (held, produced.Map!), cancellationToken).ConfigureAwait(false) is { Count: > 0 } lost)
        {
            taken.UnionWith(lost);
            produced = draw++ < Draws && work.Rerun(source with { Taken = taken.ToHashSet(StringComparer.Ordinal) }) is { Map: not null } again
                ? again
                : throw new InvalidOperationException("Every draw of the rerun met a marker another batch holds.");
        }

        return produced;
    }

    /// <summary>The map the rerun gave, written: its markers kept for the item, the old map emptied, with the event of its rerun, and the new one sealed under the key of the old one.</summary>
    internal static async Task RemapAsync(
        CustodyScope scope, (ProtectedContent Content, RerunHold Hold) of, (IReadOnlyList<NewMapEntry> Entries, ContentKey? Key) map, CancellationToken cancellationToken)
    {
        var key = map.Key ?? throw new InvalidOperationException("The rerun gave a map to an item that holds none.");
        var hold = of.Hold;
        var batch = await BatchOfAsync(scope, hold.ItemId, cancellationToken).ConfigureAwait(false);
        await KeepAsync(scope, (hold, batch), HeldOf(map.Entries), cancellationToken).ConfigureAwait(false);
        Bound[] emptied = [new("id", NpgsqlDbType.Bigint, hold.ItemId)];
        _ = await ScalarAsync(scope, (ContentQueries.EmptyReversalMaps, emptied), cancellationToken).ConfigureAwait(false);
        var entries = of.Content.Reseal(ContentColumns.Entries, hold.DomainId, key, MapEntryJson.Write(map.Entries)).Envelope.ToArray();
        Bound[] written =
        [
            new("domain", NpgsqlDbType.Integer, hold.DomainId), new("batch", NpgsqlDbType.Bigint, batch), new("item", NpgsqlDbType.Bigint, hold.ItemId),
            new("entries", NpgsqlDbType.Bytea, entries), new("epoch", NpgsqlDbType.Integer, key.Epoch), new("wrapped", NpgsqlDbType.Bytea, key.Wrapped.ToArray()),
        ];
        _ = await ScalarAsync(scope, (ContentQueries.InsertReversalMap, written), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The markers new to the map that the batch did not win now, held by another; the ones it won join the ones held.</summary>
    private static async Task<List<string>> LostAsync(
        CustodyScope scope, (int DomainId, long Batch) of, (HashSet<string> Held, IReadOnlyList<NewMapEntry> Map) markers, CancellationToken cancellationToken)
    {
        string[] fresh = [.. markers.Map.Select(entry => entry.Marker).Where(marker => !markers.Held.Contains(marker)).Distinct(StringComparer.Ordinal)];
        if (fresh.Length == 0)
        {
            return [];
        }

        Bound[] values = [new("domain", NpgsqlDbType.Integer, of.DomainId), new("batch", NpgsqlDbType.Bigint, of.Batch), new("markers", TextArray, fresh)];
        markers.Held.UnionWith(await RowsAsync(scope, (ContentQueries.InsertMarkersWon, values), row => row.GetString(0), cancellationToken).ConfigureAwait(false));
        return [.. fresh.Where(marker => !markers.Held.Contains(marker))];
    }

    /// <summary>
    /// The markers new to the item are held for its batch and given to it, the ones it held already are kept as they are, and the ones gone from its text leave it, held still.
    /// </summary>
    private static async Task KeepAsync(CustodyScope scope, (RerunHold Hold, long Batch) of, IReadOnlyList<HeldMarker> markers, CancellationToken cancellationToken)
    {
        string[] names = [.. markers.Select(marker => marker.Marker)];
        Bound[] held = [new("domain", NpgsqlDbType.Integer, of.Hold.DomainId), new("markers", TextArray, names)];
        Bound[] inBatch = [.. held, new("batch", NpgsqlDbType.Bigint, of.Batch)];
        _ = await ScalarAsync(scope, (ContentQueries.InsertMarkersInUse, inBatch), cancellationToken).ConfigureAwait(false);
        Bound[] given = [.. held, new("item", NpgsqlDbType.Bigint, of.Hold.ItemId), new("authors", BooleanArray, markers.Select(marker => marker.Author).ToArray())];
        _ = await ScalarAsync(scope, (ContentQueries.InsertItemMarkers, given), cancellationToken).ConfigureAwait(false);
        Bound[] gone = [new("item", NpgsqlDbType.Bigint, of.Hold.ItemId), new("markers", TextArray, names)];
        _ = await ScalarAsync(scope, (ContentQueries.RemoveItemMarkersGone, gone), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The batch of the item, whose conversation holds its markers.</summary>
    private static async Task<long> BatchOfAsync(CustodyScope scope, long itemId, CancellationToken cancellationToken)
    {
        Bound[] item = [new("item", NpgsqlDbType.Bigint, itemId)];
        return (long)(await ScalarAsync(scope, (ContentQueries.ReadItemBatch, item), cancellationToken).ConfigureAwait(false))!;
    }

    private static HashSet<string> MarkersOf(IReadOnlyList<NewMapEntry>? map) => new(map?.Select(entry => entry.Marker) ?? [], StringComparer.Ordinal);

    /// <summary>The markers of the map, each standing for an author when one of its entries does.</summary>
    private static List<HeldMarker> HeldOf(IEnumerable<NewMapEntry> map) =>
        [.. map.GroupBy(entry => entry.Marker, StringComparer.Ordinal).Select(same => new HeldMarker(same.Key, same.Any(entry => entry.Author)))];
}
