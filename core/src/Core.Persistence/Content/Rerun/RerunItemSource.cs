using System.Data;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Custody.Chain;
using NpgsqlTypes;
using static Core.Persistence.Content.Rerun.RerunRows;

namespace Core.Persistence.Content.Rerun;

/// <summary>
/// What the rerun reads of an item, in the transaction of the rerun: the sealed columns of the item and of its map, then the same opened, with the raw texts of its batch when it holds no
/// map and its destinations were decided, for the groups of its first pseudonymized text (issue 102, F3).
/// </summary>
internal static class RerunItemSource
{
    /// <summary>The item not emptied, sealed; none when it was emptied.</summary>
    internal static Task<List<StoredItem>> ReadAsync(CustodyScope scope, long itemId, CancellationToken cancellationToken) =>
        RowsAsync(scope, (ContentQueries.ReadResanitizationSource, [new("id", NpgsqlDbType.Bigint, itemId)]), Stored, cancellationToken);

    /// <summary>What the item holds, opened by the protected content, with the texts of its batch when it holds no map and its destinations were decided.</summary>
    internal static async Task<RerunSource> OpenedAsync(CustodyScope scope, (ProtectedContent Content, RerunHold Hold) of, StoredItem stored, CancellationToken cancellationToken)
    {
        var (content, hold) = of;
        string? Open(ProtectedColumn column, byte[]? sealedValue) => sealedValue is null ? null : content.Open(column, hold.DomainId, sealedValue, stored.Key);
        var occurrences = Open(ContentColumns.Occurrences, stored.Occurrences);
        var read = occurrences is null ? null : OccurrenceJson.Read(occurrences);
        var source = new RerunSource(Open(ContentColumns.RawText, stored.Raw)!, read, Open(ContentColumns.SanitizedText, stored.Sanitized), stored.Version)
        {
            ItemId = hold.ItemId,
            Map = stored.Entries is { } entries ? MapEntryJson.Read(content.Open(ContentColumns.Entries, hold.DomainId, entries, stored.MapKey!)) : null,
        };
        if (source.Map is not null || source.Occurrences is null)
        {
            return source;
        }

        var batch = await RowsAsync(scope, (ContentQueries.ReadBatchRawTexts, [new("id", NpgsqlDbType.Bigint, hold.ItemId)]), row => (Id: row.GetInt64(0), Raw: content.Open(
            ContentColumns.RawText, hold.DomainId, (byte[])row.GetValue(1), new ContentKey(row.GetInt32(2), (byte[])row.GetValue(3)))), cancellationToken).ConfigureAwait(false);
        return source with { Batch = [.. batch.Select(item => item.Raw)], Position = batch.FindIndex(item => item.Id == hold.ItemId) };
    }

    /// <summary>What the item holds, sealed, in the columns of the statement.</summary>
    private static StoredItem Stored(IDataRecord row)
    {
        var at = 0;
        T? Next<T>()
            where T : class => row.IsDBNull(at++) ? null : (T)row.GetValue(at - 1);
        var item = new StoredItem(Next<byte[]>()!, Next<byte[]>(), Next<byte[]>(), Next<string>(), new ContentKey(row.GetInt32(at++), Next<byte[]>()!));
        var entries = Next<byte[]>();
        return item with { Entries = entries, MapKey = entries is null ? null : new ContentKey(row.GetInt32(at), (byte[])row.GetValue(at + 1)) };
    }

    /// <summary>What the item holds, sealed, with the key of its text, and the entries of its map, sealed, with their key, none when the item is not pseudonymized.</summary>
    internal sealed record StoredItem(byte[] Raw, byte[]? Occurrences, byte[]? Sanitized, string? Version, ContentKey Key)
    {
        internal byte[]? Entries { get; init; }

        internal ContentKey? MapKey { get; init; }
    }
}
