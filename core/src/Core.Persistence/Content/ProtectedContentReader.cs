using System.Collections.Frozen;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Content;

/// <summary>
/// The one way protected content is read: the chain records who read which item, at which step, and only then is the content opened.
/// A reading the chain does not record returns no text, and a reading that finds nothing records nothing, because nothing was read.
/// The event names the item and the step and never the content.
/// </summary>
public sealed class ProtectedContentReader(NpgsqlDataSource dataSource, ProtectedContent protectedContent, CustodyChain chain)
{
    private static readonly FrozenDictionary<ProtectedColumn, ColumnReading> Readings = new Dictionary<ProtectedColumn, ColumnReading>
    {
        [ContentColumns.RawText] = new(CustodyItemKind.ItemText, CustodyStep.RawTextRead, ContentQueries.ReadRawText, ContentQueries.ReadFirstRawText),
        [ContentColumns.SanitizedText] = new(CustodyItemKind.ItemText, CustodyStep.SanitizedTextRead, ContentQueries.ReadSanitizedText),
        [ContentColumns.Entries] = new(CustodyItemKind.ReversalMap, CustodyStep.ReversalEntriesRead, ContentQueries.ReadReversalEntries, ContentQueries.ReadFirstEntries),
    }.ToFrozenDictionary();

    /// <summary>Reads the column of the row under the domain, recording the reading before the content is opened.</summary>
    /// <param name="request">Who reads, which column of which row, in which domain.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<ContentReadResult> ReadAsync(ContentRead request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ReadingAsync(request, first: false, cancellationToken);
    }

    /// <summary>Reads the column of the first row of the domain that holds a value, in one statement and one event, so that the server's log and the chain count it alike.</summary>
    /// <param name="author">Who reads; the event of the reading names them.</param>
    /// <param name="column">The protected column.</param>
    /// <param name="domainId">The domain whose first row is read.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<ContentReadResult> ReadFirstAsync(CustodyAuthor author, ProtectedColumn column, int domainId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(author);
        return ReadingAsync(new ContentRead(author, column, domainId, RowId: 0), first: true, cancellationToken);
    }

    private async Task<ContentReadResult> ReadingAsync(ContentRead request, bool first, CancellationToken cancellationToken)
    {
        var reading = Readings.GetValueOrDefault(request.Column) ?? throw new ArgumentException("The column is not a protected column of the content.", nameof(request));
        var statement = first ? reading.First ?? throw new ArgumentException("The column has no first reading.", nameof(request)) : reading.Query;
        var stored = await FetchAsync(request, statement, first, cancellationToken).ConfigureAwait(false);
        if (!stored.Found || stored.Envelope is null)
        {
            return new ContentReadResult { Outcome = stored.Found ? ContentReadOutcome.Empty : ContentReadOutcome.NotFound };
        }

        var recorded = await chain.AppendAsync(CustodyEvent.Of(request.Author, new CustodyItem(reading.Kind, stored.RowId), reading.Step, CustodyReason.ContentRead), cancellationToken)
            .ConfigureAwait(false);
        return recorded.Outcome == CustodyOutcome.Appended
            ? new ContentReadResult { Outcome = ContentReadOutcome.Read, Text = protectedContent.Open(request.Column, request.DomainId, stored.Envelope) }
            : new ContentReadResult { Outcome = ContentReadOutcome.NotRecorded, Refusal = recorded };
    }

    private async Task<StoredValue> FetchAsync(ContentRead request, string statement, bool first, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, request.DomainId, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var command = Command(transaction, statement, first ? null : request.RowId);
            await using (command.ConfigureAwait(false))
            {
                return await FirstRowAsync(command, request.RowId, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static NpgsqlCommand Command(ContentTransaction transaction, string statement, long? rowId)
    {
        var command = transaction.CreateCommand(statement);
        _ = rowId is { } id ? command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id }) : null;
        return command;
    }

    private static async Task<StoredValue> FirstRowAsync(NpgsqlCommand command, long requested, CancellationToken cancellationToken)
    {
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return new StoredValue(Found: false, 0, null);
            }

            var last = reader.FieldCount - 1;
            var sealedValue = await reader.IsDBNullAsync(last, cancellationToken).ConfigureAwait(false) ? null : (byte[])reader.GetValue(last);
            return new StoredValue(Found: true, last == 0 ? requested : reader.GetInt64(0), sealedValue);
        }
    }

    private sealed record ColumnReading(CustodyItemKind Kind, CustodyStep Step, string Query, string? First = null);

    private sealed record StoredValue(bool Found, long RowId, byte[]? Envelope);
}
