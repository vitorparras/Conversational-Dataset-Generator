using System.Data;
using System.Runtime.CompilerServices;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Chain.Append;
using Core.Persistence.People;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Content.Rerun;

/// <summary>
/// The rerun of one held item over its raw text (issue 97), in one serializable transaction: the item is read, the domain reruns it, and the event item_resanitized, the conclusion in the queue and
/// the new sanitization are written together or not at all. An identical text is not written again; an emptied item, or one whose key was declared lost, is refused with the reason; a transaction the
/// database stopped for another gives the item back without spending a try, and a label the installation did not load spends one.
/// </summary>
public sealed class ItemResanitizer(NpgsqlDataSource dataSource, ProtectedContent protectedContent, TimeProvider time)
{
    private const NpgsqlDbType IntegerArray = (NpgsqlDbType)((int)NpgsqlDbType.Array + (int)NpgsqlDbType.Integer);
    private const int SanitizedColumn = 2;
    private const int VersionColumn = 3;
    private const int EmptiedColumn = 4;
    private const int EpochColumn = 5;
    private const int WrappedColumn = 6;

    /// <summary>Reruns the held item by the rerun of the domain, given what the item holds; an exception of the rerun leaves the item as it was, held.</summary>
    public Task<ResanitizationResult> ResanitizeAsync(RerunHold hold, Func<RerunSource, Resanitized> rerun, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hold);
        ArgumentNullException.ThrowIfNull(rerun);
        return ResanitizingAsync(hold, rerun, cancellationToken);
    }

    private async Task<ResanitizationResult> ResanitizingAsync(RerunHold hold, Func<RerunSource, Resanitized> rerun, CancellationToken cancellationToken)
    {
        var treatment = new Treatment(hold, rerun, time.GetUtcNow(), new StrongBox<ResanitizationResult>(new ResanitizationResult(RerunOutcome.NotHeld)));
        var recorded = await CustodyTransaction.RunAsync(dataSource, scope => TreatInAsync(scope, treatment, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (recorded?.Outcome == CustodyOutcome.Conflict)
        {
            return new ResanitizationResult(await RerunCommands.ReturnAsync(dataSource, hold, treatment.Now, cancellationToken).ConfigureAwait(false) ? RerunOutcome.Returned : RerunOutcome.NotHeld);
        }

        if (treatment.Judged.Value!.Outcome == RerunOutcome.Failed)
        {
            var failed = await RerunCommands.FailAsync(dataSource, hold, time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            return new ResanitizationResult(failed, failed == RerunOutcome.Refused ? RerunRefusal.Failed : null);
        }

        return recorded is null or { Outcome: CustodyOutcome.Appended } ? treatment.Judged.Value : new ResanitizationResult(RerunOutcome.NotRecorded);
    }

    private async Task<CustodyResult?> TreatInAsync(CustodyScope scope, Treatment treatment, CancellationToken cancellationToken)
    {
        var hold = treatment.Hold;
        await RerunCommands.DeclareAsync(scope, hold.DomainId, cancellationToken).ConfigureAwait(false);
        var stored = await StoredAsync(scope, hold.ItemId, cancellationToken).ConfigureAwait(false);
        if (stored.Key is null)
        {
            return await FinishAsync(scope, treatment, (Refused(RerunRefusal.Emptied), CustodyReason.SanitizationRefused, null), cancellationToken).ConfigureAwait(false);
        }

        if (await SealedValueRefusal.OfAsync((dataSource, protectedContent), (stored.Raw!, stored.Key), cancellationToken).ConfigureAwait(false) is { } unopened)
        {
            treatment.Judged.Value = new ResanitizationResult(RerunOutcome.Failed);
            return unopened.Outcome == ContentReadOutcome.KeyLossDeclared
                ? await FinishAsync(scope, treatment, (Refused(RerunRefusal.KeyLost), CustodyReason.SanitizationRefused, null), cancellationToken).ConfigureAwait(false)
                : null;
        }

        var produced = treatment.Rerun(Opened(stored, hold.DomainId));
        var statement = StatementOf(produced, stored.Version);
        Func<Task>? write = statement is null ? null : () => WriteAsync(scope, statement, (hold, produced, stored.Key), cancellationToken);
        var end = produced.Identical
            ? (new ResanitizationResult(RerunOutcome.Identical), CustodyReason.SanitizationIdentical, write)
            : (new ResanitizationResult(RerunOutcome.Replaced), CustodyReason.SanitizationReplaced, write);
        return await FinishAsync(scope, treatment, end, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The event and the conclusion first, as the database asks, then the change of the item, and the commit; nothing is kept when the hold is no longer this one.</summary>
    private static async Task<CustodyResult?> FinishAsync(
        CustodyScope scope, Treatment treatment, (ResanitizationResult Result, CustodyReason Reason, Func<Task>? Write) end, CancellationToken cancellationToken)
    {
        var concluded = await RerunCommands.ConcludeAsync(scope, treatment.Hold, (end.Reason, end.Result.Refusal, treatment.Now), cancellationToken).ConfigureAwait(false);
        if (concluded is not { Outcome: CustodyOutcome.Appended })
        {
            treatment.Judged.Value = new ResanitizationResult(RerunOutcome.NotHeld);
            return concluded;
        }

        if (end.Write is not null)
        {
            await end.Write().ConfigureAwait(false);
        }

        treatment.Judged.Value = end.Result;
        return await CustodyAppend.CommitIfAppendedAsync(scope, concluded, cancellationToken).ConfigureAwait(false);
    }

    private static ResanitizationResult Refused(RerunRefusal refusal) => new(RerunOutcome.Refused, refusal);

    /// <summary>The change of the item: none for the same text of the same version, the counts and the version for the same text of another, and everything for a new text.</summary>
    private static string? StatementOf(Resanitized produced, string? storedVersion)
    {
        if (!produced.Identical)
        {
            return ContentQueries.CloseItemResanitization;
        }

        return string.Equals(produced.Version, storedVersion, StringComparison.Ordinal) ? null : ContentQueries.CloseItemRecount;
    }

    private static async Task<StoredItem> StoredAsync(CustodyScope scope, long itemId, CancellationToken cancellationToken)
    {
        var query = (ContentQueries.ReadResanitizationSource, (Action<NpgsqlCommand>)(command => PersonQueries.Add(command, "id", NpgsqlDbType.Bigint, itemId)));
        var rows = await RerunReading.RowsAsync(scope, query, Stored, cancellationToken).ConfigureAwait(false);
        return rows.Count == 0 ? StoredItem.Gone : rows[0];
    }

    private static StoredItem Stored(IDataRecord row)
    {
        if (row.GetBoolean(EmptiedColumn))
        {
            return StoredItem.Gone;
        }

        var version = row.IsDBNull(VersionColumn) ? null : row.GetString(VersionColumn);
        var key = new ContentKey(row.GetInt32(EpochColumn), (byte[])row.GetValue(WrappedColumn));
        return new StoredItem((byte[])row.GetValue(0), Sealed(row, 1), Sealed(row, SanitizedColumn), version, key);
    }

    private static byte[]? Sealed(IDataRecord row, int column) => row.IsDBNull(column) ? null : (byte[])row.GetValue(column);

    private RerunSource Opened(StoredItem stored, int domainId)
    {
        string? Open(ProtectedColumn column, byte[]? sealedValue) => sealedValue is null ? null : protectedContent.Open(column, domainId, sealedValue, stored.Key!);
        var occurrences = Open(ContentColumns.Occurrences, stored.Occurrences);
        var raw = Open(ContentColumns.RawText, stored.Raw)!;
        return new RerunSource(raw, occurrences is null ? [] : OccurrenceJson.Read(occurrences), Open(ContentColumns.SanitizedText, stored.Sanitized), stored.Version);
    }

    private async Task WriteAsync(CustodyScope scope, string statement, (RerunHold Hold, Resanitized Produced, ContentKey Key) change, CancellationToken cancellationToken)
    {
        var (hold, produced, key) = change;
        var command = scope.CreateCommand(statement);
        await using (command.ConfigureAwait(false))
        {
            PersonQueries.Add(command, "domain", NpgsqlDbType.Integer, hold.DomainId);
            PersonQueries.Add(command, "id", NpgsqlDbType.Bigint, hold.ItemId);
            PersonQueries.Add(command, "version", NpgsqlDbType.Text, produced.Version);
            PersonQueries.Add(command, "steps", NpgsqlDbType.Text, produced.Steps);
            PersonQueries.Add(command, "trail", NpgsqlDbType.Bytea, protectedContent.Reseal(ContentColumns.SanitizationTrail, hold.DomainId, key, produced.Trail).Envelope.ToArray());
            PersonQueries.Add(command, "counts", IntegerArray, produced.Counts.ToArray());
            PersonQueries.Add(command, "untreated", IntegerArray, produced.Untreated.ToArray());
            if (statement == ContentQueries.CloseItemResanitization)
            {
                PersonQueries.Add(command, "sanitized", NpgsqlDbType.Bytea, protectedContent.Reseal(ContentColumns.SanitizedText, hold.DomainId, key, produced.Text).Envelope.ToArray());
            }

            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record Treatment(RerunHold Hold, Func<RerunSource, Resanitized> Rerun, DateTimeOffset Now, StrongBox<ResanitizationResult> Judged);

    /// <summary>What the item holds, sealed; an emptied item, or one not found, holds no key.</summary>
    private sealed record StoredItem(byte[]? Raw, byte[]? Occurrences, byte[]? Sanitized, string? Version, ContentKey? Key)
    {
        internal static StoredItem Gone { get; } = new(null, null, null, null, null);
    }
}
