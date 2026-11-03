using System.Collections.Frozen;
using System.Data.Common;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Content.Reading;

/// <summary>
/// The one way protected content is read: the chain records who read which item, at which step, and only then is the content opened.
/// A reading the chain does not record returns no text, and a reading that finds nothing records nothing, because nothing was read.
/// The event names the item and the step and never the content.
/// </summary>
public sealed class ProtectedContentReader(NpgsqlDataSource dataSource, ProtectedContent protectedContent, CustodyChain chain)
{
    private static readonly FrozenDictionary<ProtectedColumn, ColumnReading> Readings = new Dictionary<ProtectedColumn, ColumnReading>
    {
        [ContentColumns.RawText] = new(CustodyItemKind.ItemText, CustodyStep.RawTextRead, ContentQueries.ReadRawText),
        [ContentColumns.SanitizedText] = new(CustodyItemKind.ItemText, CustodyStep.SanitizedTextRead, ContentQueries.ReadSanitizedText),
        [ContentColumns.Entries] = new(CustodyItemKind.ReversalMap, CustodyStep.ReversalEntriesRead, ContentQueries.ReadReversalEntries),
    }.ToFrozenDictionary();

    /// <summary>Reads the column of the row under the domain, recording the reading before the content is opened.</summary>
    /// <param name="request">Who reads, which column of which row, in which domain.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<ContentReadResult> ReadAsync(ContentRead request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ReadingAsync(request, cancellationToken);
    }

    private async Task<ContentReadResult> ReadingAsync(ContentRead request, CancellationToken cancellationToken)
    {
        var reading = Readings.GetValueOrDefault(request.Column) ?? throw new ArgumentException("The column is not a protected column of the content.", nameof(request));
        var stored = await FetchAsync(request, reading.Query, cancellationToken).ConfigureAwait(false);
        if (!stored.Found || stored.Envelope is null)
        {
            return new ContentReadResult { Outcome = Unread(stored) };
        }

        var recorded = await chain.AppendAsync(CustodyEvent.Of(request.Author, new CustodyItem(reading.Kind, request.RowId), reading.Step, CustodyReason.ContentRead), cancellationToken)
            .ConfigureAwait(false);
        return recorded.Outcome == CustodyOutcome.Appended
            ? new ContentReadResult { Outcome = ContentReadOutcome.Read, Text = protectedContent.Open(request.Column, request.DomainId, stored.Envelope) }
            : new ContentReadResult { Outcome = ContentReadOutcome.NotRecorded, Refusal = recorded };
    }

    private async Task<StoredValue> FetchAsync(ContentRead request, string statement, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, request.DomainId, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            return await ReadStoredAsync(transaction, statement, request.RowId, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<StoredValue> ReadStoredAsync(ContentTransaction transaction, string statement, long rowId, CancellationToken cancellationToken)
    {
        var command = transaction.CreateCommand(statement);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = rowId });
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await StoredOfAsync(reader, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<StoredValue> StoredOfAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new StoredValue(Found: false, null, Emptied: false);
        }

        var envelope = await reader.IsDBNullAsync(0, cancellationToken).ConfigureAwait(false) ? null : (byte[])reader.GetValue(0);
        return new StoredValue(Found: true, envelope, Emptied: reader.GetBoolean(1));
    }

    private static ContentReadOutcome Unread(StoredValue stored) => stored switch
    {
        { Found: false } => ContentReadOutcome.NotFound,
        { Emptied: true } => ContentReadOutcome.Emptied,
        _ => ContentReadOutcome.Empty,
    };

    private sealed record ColumnReading(CustodyItemKind Kind, CustodyStep Step, string Query);

    private sealed record StoredValue(bool Found, byte[]? Envelope, bool Emptied);
}
