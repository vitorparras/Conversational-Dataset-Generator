using System.Data;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Chain.Append;
using Npgsql;
using NpgsqlTypes;
using static Core.Persistence.Content.Rerun.RerunRows;

namespace Core.Persistence.Content.Rerun;

/// <summary>
/// The rerun of one held item over its raw text (issue 97), in one serializable transaction: the item is read, the domain reruns it, and the event item_resanitized, the conclusion in the queue and
/// the new sanitization are written together or not at all. An identical text is not written again; an emptied item, one whose key was declared lost or one never decided is refused with
/// the reason; a transaction the database stopped for another gives the item back without spending a try, and a label the installation did not load spends one.
/// </summary>
public sealed class ItemResanitizer
{
    private const NpgsqlDbType IntegerArray = (NpgsqlDbType)((int)NpgsqlDbType.Array + (int)NpgsqlDbType.Integer);
    private const NpgsqlDbType TextArray = (NpgsqlDbType)((int)NpgsqlDbType.Array + (int)NpgsqlDbType.Text);
    private const NpgsqlDbType BooleanArray = (NpgsqlDbType)((int)NpgsqlDbType.Array + (int)NpgsqlDbType.Boolean);

    private readonly NpgsqlDataSource dataSource;
    private readonly ProtectedContent protectedContent;
    private readonly TimeProvider time;

    /// <summary>The rerun over the database, opening and sealing the texts by the protected content, at the moments the clock gives.</summary>
    /// <param name="dataSource">The source of the connections, with the application's credential.</param>
    /// <param name="protectedContent">Opens the raw text and seals the new one with the key of the item.</param>
    /// <param name="time">The clock of the holds and the conclusions.</param>
    public ItemResanitizer(NpgsqlDataSource dataSource, ProtectedContent protectedContent, TimeProvider time)
    {
        this.dataSource = dataSource;
        this.protectedContent = protectedContent;
        this.time = time;
    }

    /// <summary>Reruns the held item by the rerun of the domain, given what the item holds; none from it refuses the item as undecided, and an exception leaves it as it was, held.</summary>
    public Task<RerunResult> ResanitizeAsync(RerunHold hold, Func<RerunSource, Resanitized?> rerun, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rerun);
        return ResanitizingAsync(hold, rerun, cancellationToken);
    }

    private async Task<RerunResult> ResanitizingAsync(RerunHold hold, Func<RerunSource, Resanitized?> rerun, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var judged = new RerunResult(RerunOutcome.NotHeld);
        var recorded = await CustodyTransaction.RunAsync(dataSource, async scope =>
        {
            (judged, var appended) = await TreatInAsync(scope, (hold, rerun, now), cancellationToken).ConfigureAwait(false);
            return appended;
        }, cancellationToken).ConfigureAwait(false);
        if (recorded?.Outcome == CustodyOutcome.Conflict)
        {
            return new RerunResult(await SpendAsync(dataSource, hold, (ContentQueries.CloseRerunReturned, RerunOutcome.Returned, now), cancellationToken).ConfigureAwait(false));
        }

        if (judged.Outcome == RerunOutcome.Failed)
        {
            var failed = await SpendAsync(dataSource, hold, (ContentQueries.CloseRerunFailed, RerunOutcome.Failed, time.GetUtcNow()), cancellationToken).ConfigureAwait(false);
            return new RerunResult(failed, Refusal: failed == RerunOutcome.Refused ? RerunRefusal.Failed : null);
        }

        return recorded is null or { Outcome: CustodyOutcome.Appended } ? judged : new RerunResult(RerunOutcome.NotRecorded);
    }

    private async Task<(RerunResult, CustodyResult?)> TreatInAsync(
        CustodyScope scope, (RerunHold Hold, Func<RerunSource, Resanitized?> Rerun, DateTimeOffset Now) work, CancellationToken cancellationToken)
    {
        var hold = work.Hold;
        await DeclareAsync(scope, hold.DomainId, cancellationToken).ConfigureAwait(false);
        var found = await RowsAsync(scope, (ContentQueries.ReadResanitizationSource, [new("id", NpgsqlDbType.Bigint, hold.ItemId)]), Stored, cancellationToken).ConfigureAwait(false);
        if (found is not [{ } stored])
        {
            return await FinishAsync(scope, work, (new RerunResult(RerunOutcome.Refused, Refusal: RerunRefusal.Emptied), null), cancellationToken).ConfigureAwait(false);
        }

        if (await SealedValueRefusal.OfAsync((dataSource, protectedContent), (stored.Raw, stored.Key), cancellationToken).ConfigureAwait(false) is { } unopened)
        {
            return unopened.Outcome == ContentReadOutcome.KeyLossDeclared
                ? await FinishAsync(scope, work, (new RerunResult(RerunOutcome.Refused, Refusal: RerunRefusal.KeyLost), null), cancellationToken).ConfigureAwait(false)
                : (new RerunResult(RerunOutcome.Failed), null);
        }

        if (work.Rerun(Opened(stored, hold.DomainId)) is not { } produced)
        {
            return await FinishAsync(scope, work, (new RerunResult(RerunOutcome.Refused, Refusal: RerunRefusal.Undecided), null), cancellationToken).ConfigureAwait(false);
        }

        var statement = StatementOf(produced, stored.Version);
        Func<Task>? write = statement is null && produced.Markers is null ? null : () => WriteAsync(scope, statement, (hold, produced, stored.Key), cancellationToken);
        return await FinishAsync(scope, work, (new RerunResult(produced.Identical ? RerunOutcome.Identical : RerunOutcome.Replaced), write), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The event and the conclusion in the queue first, as the database asks, then the change of the item, and the commit; nothing is kept when the hold is no longer this one.</summary>
    private static async Task<(RerunResult, CustodyResult?)> FinishAsync(
        CustodyScope scope, (RerunHold Hold, Func<RerunSource, Resanitized?> Rerun, DateTimeOffset Now) work, (RerunResult Result, Func<Task>? Write) end, CancellationToken cancellationToken)
    {
        var (name, reason) = Concluded(end.Result.Outcome);
        var appended = await CustodyAppend.AppendAsync(scope, CustodyEvent.ItemResanitized(work.Hold.ItemId, reason, work.Hold.Version), cancellationToken).ConfigureAwait(false);
        Bound[] closing = [.. Of(work.Hold, work.Now), new("result", NpgsqlDbType.Text, name), new("refusal", NpgsqlDbType.Text, end.Result.Refusal is { } refusal ? NameOf(refusal) : null)];
        if (appended.Outcome != CustodyOutcome.Appended || await ScalarAsync(scope, (ContentQueries.CloseRerunItem, closing), cancellationToken).ConfigureAwait(false) is null)
        {
            return (new RerunResult(RerunOutcome.NotHeld), appended.Outcome == CustodyOutcome.Appended ? null : appended);
        }

        if (end.Write is not null)
        {
            await end.Write().ConfigureAwait(false);
        }

        return (end.Result, await CustodyAppend.CommitIfAppendedAsync(scope, appended, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The name the queue keeps for the result, and the reason of its event.</summary>
    private static (string Name, CustodyReason Reason) Concluded(RerunOutcome outcome)
    {
        if (outcome == RerunOutcome.Identical)
        {
            return ("identical", CustodyReason.SanitizationIdentical);
        }

        return outcome == RerunOutcome.Replaced ? ("replaced", CustodyReason.SanitizationReplaced) : ("refused", CustodyReason.SanitizationRefused);
    }

    /// <summary>The change of the item: none for the same text of the same version, the counts and the version for the same text of another, and everything for a new text.</summary>
    private static string? StatementOf(Resanitized produced, string? storedVersion)
    {
        if (!produced.Identical)
        {
            return ContentQueries.CloseItemResanitization;
        }

        return string.Equals(produced.Version, storedVersion, StringComparison.Ordinal) ? null : ContentQueries.CloseItemRecount;
    }

    /// <summary>What the item holds, sealed, in the columns of the statement.</summary>
    private static StoredItem Stored(IDataRecord row)
    {
        var at = 0;
        T? Next<T>()
            where T : class => row.IsDBNull(at++) ? null : (T)row.GetValue(at - 1);
        return new StoredItem(Next<byte[]>()!, Next<byte[]>(), Next<byte[]>(), Next<string>(), new ContentKey(row.GetInt32(at++), Next<byte[]>()!));
    }

    private RerunSource Opened(StoredItem stored, int domainId)
    {
        string? Open(ProtectedColumn column, byte[]? sealedValue) => sealedValue is null ? null : protectedContent.Open(column, domainId, sealedValue, stored.Key);
        var occurrences = Open(ContentColumns.Occurrences, stored.Occurrences);
        var raw = Open(ContentColumns.RawText, stored.Raw)!;
        return new RerunSource(raw, occurrences is null ? null : OccurrenceJson.Read(occurrences), Open(ContentColumns.SanitizedText, stored.Sanitized), stored.Version);
    }

    private async Task WriteAsync(CustodyScope scope, string? statement, (RerunHold Hold, Resanitized Produced, ContentKey Key) change, CancellationToken cancellationToken)
    {
        var (hold, produced, key) = change;
        if (produced.Markers is { } markers)
        {
            await KeepMarkersAsync(scope, hold, markers, cancellationToken).ConfigureAwait(false);
        }

        if (statement is null)
        {
            return;
        }

        byte[] Sealed(ProtectedColumn column, string text) => protectedContent.Reseal(column, hold.DomainId, key, text).Envelope.ToArray();
        Bound[] values =
        [
            new("domain", NpgsqlDbType.Integer, hold.DomainId), new("id", NpgsqlDbType.Bigint, hold.ItemId), new("version", NpgsqlDbType.Text, produced.Version),
            new("steps", NpgsqlDbType.Text, produced.Steps), new("trail", NpgsqlDbType.Bytea, Sealed(ContentColumns.SanitizationTrail, produced.Trail)),
            new("counts", IntegerArray, produced.Counts.ToArray()), new("untreated", IntegerArray, produced.Untreated.ToArray()),
            new("sanitized", NpgsqlDbType.Bytea, statement == ContentQueries.CloseItemResanitization ? Sealed(ContentColumns.SanitizedText, produced.Text) : null),
        ];
        _ = await ScalarAsync(scope, (statement, values), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The markers new to the item are held and given to it, the ones it held already are kept as they are, and the ones gone from its text leave it, held still.</summary>
    private static async Task KeepMarkersAsync(CustodyScope scope, RerunHold hold, IReadOnlyList<HeldMarker> markers, CancellationToken cancellationToken)
    {
        string[] names = [.. markers.Select(marker => marker.Marker)];
        Bound[] held = [new("domain", NpgsqlDbType.Integer, hold.DomainId), new("markers", TextArray, names)];
        _ = await ScalarAsync(scope, (ContentQueries.InsertMarkersInUse, held), cancellationToken).ConfigureAwait(false);
        Bound[] given = [.. held, new("item", NpgsqlDbType.Bigint, hold.ItemId), new("authors", BooleanArray, markers.Select(marker => marker.Author).ToArray())];
        _ = await ScalarAsync(scope, (ContentQueries.InsertItemMarkers, given), cancellationToken).ConfigureAwait(false);
        Bound[] gone = [new("item", NpgsqlDbType.Bigint, hold.ItemId), new("markers", TextArray, names)];
        _ = await ScalarAsync(scope, (ContentQueries.RemoveItemMarkersGone, gone), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>What the item holds, sealed, with the key of its text.</summary>
    private sealed record StoredItem(byte[] Raw, byte[]? Occurrences, byte[]? Sanitized, string? Version, ContentKey Key);
}
