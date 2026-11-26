using System.Diagnostics;
using Core.Persistence.Content.Reading;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;
using static Core.Persistence.Content.Rerun.RerunRows;

namespace Core.Persistence.Content.Reversal;

/// <summary>
/// The revelation of the marker of a request to who asked it: the revelation and its event are committed first, and only then the map of the item is opened, so that no identity
/// leaves without its record; an authorization lapsed or used, or an item emptied, is denied with the reason, and the denial is an event of its own.
/// </summary>
public sealed class ReversalRevelations
{
    private readonly NpgsqlDataSource dataSource;
    private readonly ProtectedContentReader reader;
    private readonly TimeProvider time;

    /// <summary>The revelations over the database, the map opened through the reading of protected content, judged at the moments the clock gives.</summary>
    /// <param name="dataSource">The source of the connections, with the application's credential.</param>
    /// <param name="reader">Opens the map of the item, once the chain recorded who read it.</param>
    /// <param name="time">The clock that says whether the authorization is in force.</param>
    public ReversalRevelations(NpgsqlDataSource dataSource, ProtectedContentReader reader, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(time);
        (this.dataSource, this.reader, this.time) = (dataSource, reader, time);
    }

    /// <summary>Reveals the marker of the request to who asked it, once; a request of another, or none, is not found, and one not authorized is closed, both with no event.</summary>
    /// <param name="act">Who reveals which request of which domain.</param>
    /// <param name="cancellationToken">Cancels the revelation.</param>
    public Task<ReversalRevelation> RevealAsync(ReversalAct act, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(act);
        return RevealInAsync(act, cancellationToken);
    }

    private static async Task<(ReversalResult, CustodyEvent?)> JudgeAsync(CustodyScope scope, ReversalAct act, ListedReversal? request, CancellationToken cancellationToken)
    {
        if (request is null || request.RequestedBy != act.PersonId)
        {
            return (new ReversalResult(ReversalOutcome.NotFound), null);
        }

        var authorization = request.AuthorizationId.GetValueOrDefault();
        var held = await HeldAsync(scope, request, cancellationToken).ConfigureAwait(false);
        return request.State switch
        {
            ReversalState.Authorized when held => await WriteAsync(scope, act, authorization, cancellationToken).ConfigureAwait(false),
            ReversalState.Authorized => Denied(ReversalOutcome.Emptied, CustodyReason.ReversalEmptied),
            ReversalState.Expired => Denied(ReversalOutcome.Expired, CustodyReason.ReversalExpired),
            ReversalState.Revealed => Denied(ReversalOutcome.AlreadyRevealed, CustodyReason.ReversalUsed),
            ReversalState.Pending or ReversalState.Declined => (new ReversalResult(ReversalOutcome.Closed), null),
            _ => throw new UnreachableException(),
        };

        (ReversalResult, CustodyEvent?) Denied(ReversalOutcome outcome, CustodyReason reason) =>
            (new ReversalResult(outcome), CustodyEvent.ReversalDenied(act.PersonId, authorization, reason));
    }

    /// <summary>Whether the request is authorized and its item still holds its text, and so its map.</summary>
    private static async Task<bool> HeldAsync(CustodyScope scope, ListedReversal request, CancellationToken cancellationToken) =>
        request.State == ReversalState.Authorized
        && await ScalarAsync(scope, (ContentQueries.ReadItemHeld, [new("item", NpgsqlDbType.Bigint, request.ItemId)]), cancellationToken).ConfigureAwait(false) is true;

    /// <summary>The revelation by the authorization, with its event; another that took the authorization first is a conflict, and the next attempt finds it used.</summary>
    private static async Task<(ReversalResult, CustodyEvent?)> WriteAsync(CustodyScope scope, ReversalAct act, long authorizationId, CancellationToken cancellationToken)
    {
        Bound[] row = [new("domain", NpgsqlDbType.Integer, act.DomainId), new("authorization", NpgsqlDbType.Bigint, authorizationId)];
        var written = await ScalarAsync(scope, (ContentQueries.InsertReversalRevelation, row), cancellationToken).ConfigureAwait(false);
        return written is long id
            ? (new ReversalResult(ReversalOutcome.Recorded), CustodyEvent.ReversalRevealed(act.PersonId, id, authorizationId))
            : (new ReversalResult(ReversalOutcome.NotRecorded) { Refusal = CustodyRefusals.Conflict() }, null);
    }

    private async Task<ReversalRevelation> RevealInAsync(ReversalAct act, CancellationToken cancellationToken)
    {
        ListedReversal? request = null;
        var recorded = await ReversalAttempts.RetriedAsync(dataSource, async scope =>
        {
            await DeclareAsync(scope, act.DomainId, cancellationToken).ConfigureAwait(false);
            request = await ReversalStanding.ReadAsync(scope, act.RequestId, time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            return await JudgeAsync(scope, act, request, cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
        return recorded.Outcome == ReversalOutcome.Recorded
            ? await OpenAsync(act, request!, cancellationToken).ConfigureAwait(false)
            : new ReversalRevelation(recorded.Outcome) { Refusal = recorded.Refusal };
    }

    /// <summary>Opens the map of the item, now that the revelation is committed, and finds the original of the marker in it; the reading is an event of its own, before the value.</summary>
    private async Task<ReversalRevelation> OpenAsync(ReversalAct act, ListedReversal request, CancellationToken cancellationToken)
    {
        var (at, map) = await InDomainAsync(act.DomainId, async scope => (
            (await ReversalStanding.ReadAsync(scope, request.Id, time.GetUtcNow(), cancellationToken).ConfigureAwait(false))?.RevealedAt,
            await ScalarAsync(scope, (ContentQueries.ReadMapOfItem, [new("item", NpgsqlDbType.Bigint, request.ItemId)]), cancellationToken).ConfigureAwait(false)),
            cancellationToken).ConfigureAwait(false);
        if (map is not long mapId)
        {
            return new ReversalRevelation(ReversalOutcome.Emptied) { RevealedAt = at };
        }

        var read = await ReadMapAsync(new ContentRead(CustodyAuthor.Person(act.PersonId), ContentColumns.Entries, act.DomainId, mapId), cancellationToken).ConfigureAwait(false);
        return read.Outcome == ContentReadOutcome.Read
            ? new ReversalRevelation(ReversalOutcome.Recorded) { RevealedAt = at, Identity = MapEntryJson.OriginalOf(read.Text!, request.Marker) }
            : new ReversalRevelation(ReversalOutcome.NotRecorded) { RevealedAt = at, Refusal = read.Refusal };
    }

    /// <summary>
    /// Reads the map, again while the chain refuses the reading as a conflict with another writer, up to three times: the revelation is committed already, and a conflict is
    /// transient; a chain broken is not tried again.
    /// </summary>
    private async Task<ContentReadResult> ReadMapAsync(ContentRead map, CancellationToken cancellationToken)
    {
        var tries = 0;
        ContentReadResult read;
        do
        {
            read = await reader.ReadAsync(map, cancellationToken).ConfigureAwait(false);
        }
        while (read.Refusal?.Outcome == CustodyOutcome.Conflict && ++tries < ReversalAttempts.Attempts);

        return read;
    }

    private async Task<T> InDomainAsync<T>(int domainId, Func<CustodyScope, Task<T>> read, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, domainId, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            return await read(transaction.AsCustodyScope()).ConfigureAwait(false);
        }
    }
}
