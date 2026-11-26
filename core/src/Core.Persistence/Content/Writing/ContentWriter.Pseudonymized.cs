using System.Data;
using System.Data.Common;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Content.Writing;

/// <summary>
/// The writing of a pseudonymized item: the item, its map and its markers in one serializable transaction, or nothing (issue 101); and the markers of a conversation held for its batch
/// before its first item, all of them or none (issue 102).
/// </summary>
public sealed partial class ContentWriter
{
    private const NpgsqlDbType TextArray = (NpgsqlDbType)((int)NpgsqlDbType.Array + (int)NpgsqlDbType.Text);
    private const NpgsqlDbType BooleanArray = (NpgsqlDbType)((int)NpgsqlDbType.Array + (int)NpgsqlDbType.Boolean);

    /// <summary>
    /// Adds a pseudonymized item to an open batch of the domain with the map that reverses it and the markers it holds, in one transaction: the pseudonymized text and the
    /// map are sealed, the markers are held in clear in the installation for the batch. When another batch holds some marker already, nothing is written and the markers taken are named;
    /// a marker the batch holds, won by an earlier message of the same conversation, is the item's too (0057).
    /// </summary>
    /// <param name="domainId">The domain the item belongs to.</param>
    /// <param name="batchId">The batch the item comes in with, which must be open.</param>
    /// <param name="item">The texts of the item, the pseudonymized among them, and the entries of its map, every one of an occurrence to pseudonymize.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    public Task<PseudonymizedWrite> AddPseudonymizedItemAsync(int domainId, long batchId, NewItemText item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Pseudonymized is null || item.Map is not { } map)
        {
            throw new ArgumentException("A pseudonymized item comes with its text and its map.", nameof(item));
        }

        return map.Any(entry => entry.Destination != OccurrenceDestination.Pseudonymize)
            ? throw new ArgumentException("Only an occurrence to pseudonymize becomes an entry of the map.", nameof(item))
            : AddPseudonymizedAsync(domainId, batchId, item, cancellationToken);
    }

    /// <summary>
    /// Holds for the batch the markers drawn for its conversation, before any item of it is written, in one transaction: all of them, or none when another batch holds some, which are
    /// named for the conversation to be drawn again without them (0057). A marker the batch holds already is held still.
    /// </summary>
    /// <param name="domainId">The domain of the batch.</param>
    /// <param name="batchId">The batch the conversation came in with.</param>
    /// <param name="markers">The markers the swap gave the items of the conversation.</param>
    /// <param name="cancellationToken">Cancels the reservation.</param>
    public Task<IReadOnlyList<string>> ReserveMarkersAsync(int domainId, long batchId, IReadOnlyList<string> markers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(markers);
        string[] given = [.. markers];
        return WriteAsync<IReadOnlyList<string>>(
            (domainId, IsolationLevel.Serializable),
            async transaction =>
            {
                var kept = await HeldAsync(transaction, (domainId, batchId), given, cancellationToken).ConfigureAwait(false);
                List<string> taken = [.. given.Where(marker => !kept.Contains(marker))];
                if (taken.Count == 0)
                {
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }

                return taken;
            },
            cancellationToken);
    }

    private async Task<PseudonymizedWrite> AddPseudonymizedAsync(int domainId, long batchId, NewItemText item, CancellationToken cancellationToken)
    {
        using var itemSealing = protectedContent.Begin(ContentColumns.ItemKey, domainId);
        using var mapSealing = protectedContent.Begin(ContentColumns.MapKey, domainId);
        var entries = mapSealing.Seal(ContentColumns.Entries, MapEntryJson.Write(item.Map!));
        var held = item.Map!.GroupBy(entry => entry.Marker, StringComparer.Ordinal).Select(same => new HeldMarker(same.Key, same.Any(entry => entry.Author))).ToList();
        var pending = new PendingItem(batchId, ItemParameters(batchId, item, itemSealing), () => [Bytes("entries", entries), .. KeyOf(mapSealing)], held);
        return await WriteAsync((domainId, IsolationLevel.Serializable), transaction => WriteInAsync(transaction, domainId, pending, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>The item, then its map, then the markers held in the installation; a marker passed over rolls everything back, and the markers of the item come last.</summary>
    private static async Task<PseudonymizedWrite> WriteInAsync(ContentTransaction transaction, int domainId, PendingItem pending, CancellationToken cancellationToken)
    {
        var itemId = (long)(await ScalarAsync(transaction, domainId, (ContentQueries.InsertItemText, pending.Item()), cancellationToken).ConfigureAwait(false))!;
        NpgsqlParameter[] map = [Batch(pending.BatchId), ItemOf(itemId), .. pending.Map()];
        _ = await ScalarAsync(transaction, domainId, (ContentQueries.InsertReversalMap, map), cancellationToken).ConfigureAwait(false);
        string[] markers = [.. pending.Held.Select(marker => marker.Marker)];
        var kept = await HeldAsync(transaction, (domainId, pending.BatchId), markers, cancellationToken).ConfigureAwait(false);
        if (markers.Where(marker => !kept.Contains(marker)).ToList() is { Count: > 0 } taken)
        {
            return new PseudonymizedWrite(null, taken);
        }

        NpgsqlParameter[] items = [ItemOf(itemId), MarkersOf(markers), new("authors", BooleanArray) { Value = pending.Held.Select(marker => marker.Author).ToArray() }];
        _ = await ScalarAsync(transaction, domainId, (ContentQueries.InsertItemMarkers, items), cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new PseudonymizedWrite(itemId, []);
    }

    /// <summary>The markers the batch holds among the ones given, won now or before; the ones another batch holds are not among them.</summary>
    private static async Task<HashSet<string>> HeldAsync(ContentTransaction transaction, (int DomainId, long BatchId) of, string[] markers, CancellationToken cancellationToken)
    {
        var command = transaction.CreateCommand(ContentQueries.InsertMarkersInUse);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("domain", NpgsqlDbType.Integer) { Value = of.DomainId });
            _ = command.Parameters.Add(Batch(of.BatchId));
            _ = command.Parameters.Add(MarkersOf(markers));
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await HeldOfAsync(reader, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<HashSet<string>> HeldOfAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var held = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            _ = held.Add(reader.GetString(0));
        }

        return held;
    }

    private static NpgsqlParameter ItemOf(long itemId) => new("item", NpgsqlDbType.Bigint) { Value = itemId };

    private static NpgsqlParameter MarkersOf(string[] markers) => new("markers", TextArray) { Value = markers };

    /// <summary>What the transaction writes, sealed already: the parameters of the item and of the map, and the markers the item holds.</summary>
    private sealed record PendingItem(long BatchId, Func<NpgsqlParameter[]> Item, Func<NpgsqlParameter[]> Map, IReadOnlyList<HeldMarker> Held);
}
