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
/// The event names the item and the step and never the content. A reading of what is sealed under a key whose loss was declared, or under a label not loaded, is refused first and records nothing.
/// </summary>
public sealed class ProtectedContentReader(NpgsqlDataSource dataSource, ProtectedContent protectedContent, CustodyChain chain)
{
    private const int EpochColumn = 2;
    private const int WrappedColumn = 3;
    private const int FormColumn = 4;
    private const int LossesColumn = 5;

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

    /// <summary>
    /// Reads the column of the first row of the domain that holds a value and comes after the row given, in one statement and one event, so that the server's log and the chain count it alike.
    /// </summary>
    /// <param name="after">Who reads, which column, in which domain, and the row after which to look: the <see cref="ContentRead.RowId" />, 0 for the first row of the domain.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<ContentReadResult> ReadFirstAsync(ContentRead after, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(after);
        return ReadingAsync(after, first: true, cancellationToken);
    }

    private async Task<ContentReadResult> ReadingAsync(ContentRead request, bool first, CancellationToken cancellationToken)
    {
        var reading = Readings.GetValueOrDefault(request.Column) ?? throw new ArgumentException("The column is not a protected column of the content.", nameof(request));
        var statement = first ? reading.First ?? throw new ArgumentException("The column has no first reading.", nameof(request)) : reading.Query;
        var stored = await FetchAsync(request, new Lookup(statement, request.RowId, first), cancellationToken).ConfigureAwait(false);
        if (!stored.Found || stored.Envelope is null)
        {
            return new ContentReadResult { Outcome = Unread(stored) };
        }

        if (await RefusedBeforeRecordingAsync(stored, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused with { RowId = stored.RowId };
        }

        var recorded = await chain.AppendAsync(CustodyEvent.Of(request.Author, new CustodyItem(reading.Kind, stored.RowId), reading.Step, CustodyReason.ContentRead), cancellationToken)
            .ConfigureAwait(false);
        return recorded.Outcome == CustodyOutcome.Appended
            ? new ContentReadResult { Outcome = ContentReadOutcome.Read, Text = protectedContent.Open(request.Column, request.DomainId, stored.Envelope, stored.Key!),
                RowId = stored.RowId, Accommodation = stored.Accommodation }
            : new ContentReadResult { Outcome = ContentReadOutcome.NotRecorded, Refusal = recorded, RowId = stored.RowId };
    }

    /// <summary>What a reading is told before anything is recorded: the loss of the key it is sealed under, which was declared, or a label the installation does not hold.</summary>
    private async Task<ContentReadResult?> RefusedBeforeRecordingAsync(StoredValue stored, CancellationToken cancellationToken)
    {
        var lost = await DeclaredKeyLoss.ScopesAsync(dataSource, cancellationToken).ConfigureAwait(false);
        var carried = ProtectedContent.LabelsCarried(stored.Envelope!, stored.Key!);
        var scope = lost.Contains(KeyLossScope.MasterText) ? KeyLossScope.MasterText : carried.FirstOrDefault(lost.Contains);
        if (scope is not null)
        {
            return new ContentReadResult { Outcome = ContentReadOutcome.KeyLossDeclared, Label = scope };
        }

        var missing = carried.FirstOrDefault(label => !protectedContent.IsLoaded(label));
        return missing is null ? null : new ContentReadResult { Outcome = ContentReadOutcome.LabelMissing, Label = missing };
    }

    private async Task<StoredValue> FetchAsync(ContentRead request, Lookup lookup, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, request.DomainId, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            return await ReadStoredAsync(transaction, lookup, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<StoredValue> ReadStoredAsync(ContentTransaction transaction, Lookup lookup, CancellationToken cancellationToken)
    {
        var command = Command(transaction, lookup);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await StoredOfAsync(reader, lookup, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    // The id is the row to read, or, for the first reading, the row after which to look.
    private static NpgsqlCommand Command(ContentTransaction transaction, Lookup lookup)
    {
        var command = transaction.CreateCommand(lookup.Statement);
        _ = command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = lookup.Id });
        return command;
    }

    // The first reading selects (id, value) among the rows not emptied; the reading of one row selects (value, emptied).
    private static async Task<StoredValue> StoredOfAsync(DbDataReader reader, Lookup lookup, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new StoredValue(Found: false, 0, null, Emptied: false);
        }

        var first = lookup.First;
        var valueAt = first ? 1 : 0;
        var held = !await reader.IsDBNullAsync(valueAt, cancellationToken).ConfigureAwait(false);
        return new StoredValue(
            Found: true,
            first ? reader.GetInt64(0) : lookup.Id,
            held ? (byte[])reader.GetValue(valueAt) : null,
            Emptied: !first && reader.GetBoolean(1),
            held ? new ContentKey(reader.GetInt32(EpochColumn), (byte[])reader.GetValue(WrappedColumn)) : null,
            await AccommodationOfAsync(reader, cancellationToken).ConfigureAwait(false));
    }

    // The reading of an item selects, after its key, the form it received and the codes of its losses; the reading of a map or of an origin selects neither.
    private static async Task<ItemAccommodation?> AccommodationOfAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        if (reader.FieldCount <= LossesColumn || await reader.IsDBNullAsync(FormColumn, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new ItemAccommodation(reader.GetString(FormColumn), reader.GetFieldValue<string[]>(LossesColumn));
    }

    private static ContentReadOutcome Unread(StoredValue stored) => stored switch
    {
        { Found: false } => ContentReadOutcome.NotFound,
        { Emptied: true } => ContentReadOutcome.Emptied,
        _ => ContentReadOutcome.Empty,
    };

    private sealed record ColumnReading(CustodyItemKind Kind, CustodyStep Step, string Query, string? First = null);

    private sealed record Lookup(string Statement, long Id, bool First);

    private sealed record StoredValue(bool Found, long RowId, byte[]? Envelope, bool Emptied, ContentKey? Key = null, ItemAccommodation? Accommodation = null);
}
