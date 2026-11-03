using System.Data;
using System.Diagnostics;
using Core.Persistence.Content.Emptying;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Content;

/// <summary>
/// Adds content to the database, and is the only way to: what a protected column receives is sealed here for the domain of the row, so the
/// database never holds the text of a protected column in clear.
/// </summary>
public sealed class ContentWriter(NpgsqlDataSource dataSource, ProtectedContent protectedContent)
{
    /// <summary>How many times a map is written when the database serializes it behind an emptying that committed first; the next attempt finds the item emptied and is refused.</summary>
    private const int MapAttempts = 3;

    /// <summary>Adds an item to the domain and answers its identifier.</summary>
    /// <param name="domainId">The domain the item belongs to.</param>
    /// <param name="item">The texts of the item.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    public Task<long> AddItemTextAsync(int domainId, NewItemText item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        return AddItemAsync(domainId, item, cancellationToken);
    }

    /// <summary>Adds the map that reverses the pseudonyms of an item of the domain and answers its identifier.</summary>
    /// <param name="domainId">The domain of the item and of the map.</param>
    /// <param name="itemId">The item the map belongs to; an item of another domain is refused by the database.</param>
    /// <param name="entries">What each pseudonym replaced.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    public Task<long> AddReversalMapAsync(int domainId, long itemId, string entries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return AddMapAsync(domainId, itemId, entries, cancellationToken);
    }

    private async Task<long> AddItemAsync(int domainId, NewItemText item, CancellationToken cancellationToken)
    {
        var raw = protectedContent.Seal(ContentColumns.RawText, domainId, item.Raw);
        var sanitized = item.Sanitized is null ? null : protectedContent.Seal(ContentColumns.SanitizedText, domainId, item.Sanitized);
        return await InsertAsync(
            domainId,
            new Insertion(ContentQueries.InsertItemText, () => [Bytes("raw", raw), Bytes("sanitized", sanitized), Text("pseudonymized", item.Pseudonymized)], IsolationLevel.ReadCommitted),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<long> AddMapAsync(int domainId, long itemId, string entries, CancellationToken cancellationToken)
    {
        var sealedEntries = protectedContent.Seal(ContentColumns.Entries, domainId, entries);
        var insertion = new Insertion(
            ContentQueries.InsertReversalMap,
            () => [new NpgsqlParameter("item", NpgsqlDbType.Bigint) { Value = itemId }, Bytes("entries", sealedEntries)],
            IsolationLevel.Serializable);
        return await InsertAsync(domainId, insertion, cancellationToken).ConfigureAwait(false);
    }

    private static NpgsqlParameter Bytes(string name, SealedContent? content) =>
        new(name, NpgsqlDbType.Bytea) { Value = content is null ? DBNull.Value : content.Envelope.ToArray() };

    private static NpgsqlParameter Text(string name, string? value) =>
        new(name, NpgsqlDbType.Text) { Value = (object?)value ?? DBNull.Value };

    /// <summary>
    /// A map is written serializable, like the emptying that reads it: a map committed while an item is being emptied would stay whole in an item that was emptied, and the database
    /// stops one of the two. The map is written again, and then it finds the item emptied.
    /// </summary>
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
