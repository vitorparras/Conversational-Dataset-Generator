using System.Data;
using System.Diagnostics;
using Core.Persistence.Content.Emptying;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Content.Writing;

/// <summary>
/// Adds content to the database, and is the only way to: what a protected column receives is sealed here for the domain of the row, so the
/// database never holds the text of a protected column in clear.
/// </summary>
public sealed class ContentWriter(NpgsqlDataSource dataSource, ProtectedContent protectedContent)
{
    /// <summary>How many times a map is written when the database serializes it behind an emptying that committed first; the next attempt finds the item emptied and is refused.</summary>
    private const int MapAttempts = 3;

    /// <summary>Adds an item to a batch of the domain, which must be open, and answers its identifier.</summary>
    /// <param name="domainId">The domain the item belongs to.</param>
    /// <param name="batchId">The batch the item comes in with; the database refuses a batch that is not open, or that is of another domain.</param>
    /// <param name="item">The texts of the item.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    public Task<long> AddItemTextAsync(int domainId, long batchId, NewItemText item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        return AddItemAsync(domainId, batchId, item, cancellationToken);
    }

    /// <summary>Adds the map that reverses the pseudonyms of an item of the domain, in the batch of the item, and answers its identifier.</summary>
    /// <param name="domainId">The domain of the item and of the map.</param>
    /// <param name="map">The batch, the item the map belongs to and what each pseudonym replaced.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    public Task<long> AddReversalMapAsync(int domainId, NewReversalMap map, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(map.Entries);
        return AddMapAsync(domainId, map, cancellationToken);
    }

    private async Task<long> AddItemAsync(int domainId, long batchId, NewItemText item, CancellationToken cancellationToken)
    {
        using var sealing = protectedContent.Begin(ContentColumns.ItemKey, domainId);
        var raw = sealing.Seal(ContentColumns.RawText, item.Raw);
        var sanitized = item.Sanitized is null ? null : sealing.Seal(ContentColumns.SanitizedText, item.Sanitized);
        var insertion = new Insertion(
            ContentQueries.InsertItemText,
            () => [Batch(batchId), Bytes("raw", raw), Bytes("sanitized", sanitized), Text("pseudonymized", item.Pseudonymized), .. KeyOf(sealing), .. Declared(item.Accommodation)],
            IsolationLevel.ReadCommitted);
        return await InsertAsync(domainId, insertion, cancellationToken).ConfigureAwait(false);
    }

    private static IEnumerable<NpgsqlParameter> Declared(ItemAccommodation? accommodation)
    {
        yield return Text("accommodated", accommodation?.Form);
        yield return new NpgsqlParameter("losses", NpgsqlDbType.Text) { Value = accommodation is null ? string.Empty : string.Join(',', accommodation.Losses) };
    }

    private async Task<long> AddMapAsync(int domainId, NewReversalMap map, CancellationToken cancellationToken)
    {
        using var sealing = protectedContent.Begin(ContentColumns.MapKey, domainId);
        var sealedEntries = sealing.Seal(ContentColumns.Entries, map.Entries);
        var insertion = new Insertion(
            ContentQueries.InsertReversalMap,
            () => [Batch(map.BatchId), new NpgsqlParameter("item", NpgsqlDbType.Bigint) { Value = map.ItemId }, Bytes("entries", sealedEntries), .. KeyOf(sealing)],
            IsolationLevel.Serializable);
        return await InsertAsync(domainId, insertion, cancellationToken).ConfigureAwait(false);
    }

    private static NpgsqlParameter Batch(long batchId) => new("batch", NpgsqlDbType.Bigint) { Value = batchId };

    private static IEnumerable<NpgsqlParameter> KeyOf(ItemSealing sealing)
    {
        yield return new NpgsqlParameter("epoch", NpgsqlDbType.Integer) { Value = sealing.Stored.Epoch };
        yield return new NpgsqlParameter("wrapped", NpgsqlDbType.Bytea) { Value = sealing.Stored.Wrapped.ToArray() };
    }

    private static NpgsqlParameter Bytes(string name, SealedContent? content) =>
        new(name, NpgsqlDbType.Bytea) { Value = content is null ? DBNull.Value : content.Envelope.ToArray() };

    private static NpgsqlParameter Text(string name, string? value) =>
        new(name, NpgsqlDbType.Text) { Value = (object?)value ?? DBNull.Value };

    /// <summary>A map is written serializable, like the emptying that reads it: the database stops one of the two, and the map is written again, finding the item emptied.</summary>
    private async Task<long> InsertAsync(int domainId, Insertion insertion, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MapAttempts; attempt++)
        {
            try
            {
                return await InsertInTransactionAsync(domainId, insertion, cancellationToken).ConfigureAwait(false);
            }
            catch (PostgresException refused) when (refused.SqlState == CustodyStatements.ChainBrokenSqlState)
            {
                throw new CustodyChainBrokenException();
            }
            catch (PostgresException refused) when (refused.SqlState == CustodyStatements.EmptiedSqlState)
            {
                throw new ItemEmptiedException();
            }
            catch (PostgresException refused) when (refused.SqlState == CustodyStatements.BatchSqlState)
            {
                throw new BatchNotOpenException();
            }
            catch (PostgresException refused) when (refused.SqlState == PostgresErrorCodes.SerializationFailure && attempt < MapAttempts)
            {
                // The database serialized this write behind another; the attempt that follows sees what the other committed.
            }
        }

        throw new UnreachableException();
    }

    private async Task<long> InsertInTransactionAsync(int domainId, Insertion insertion, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, domainId, insertion.Isolation, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var command = transaction.CreateCommand(insertion.Statement);
            await using (command.ConfigureAwait(false))
            {
                _ = command.Parameters.Add(new NpgsqlParameter("domain", NpgsqlDbType.Integer) { Value = domainId });
                command.Parameters.AddRange(insertion.Parameters());
                var id = (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return id;
            }
        }
    }

    private sealed record Insertion(string Statement, Func<NpgsqlParameter[]> Parameters, IsolationLevel Isolation);
}
