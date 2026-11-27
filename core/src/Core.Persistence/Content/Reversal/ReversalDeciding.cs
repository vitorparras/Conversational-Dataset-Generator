using System.Diagnostics;
using Core.Ingestion.Contracts.Reversal;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;
using static Core.Persistence.Content.Rerun.RerunRows;

namespace Core.Persistence.Content.Reversal;

/// <summary>The decision on a request of the domain: an authorization until the moment given, or, with none, a decline.</summary>
internal sealed class ReversalDeciding
{
    private readonly NpgsqlDataSource dataSource;
    private readonly TimeProvider time;

    internal ReversalDeciding(NpgsqlDataSource dataSource, TimeProvider time)
    {
        (this.dataSource, this.time) = (dataSource, time);
    }

    internal Task<ReversalResult> DecideAsync(ReversalAct act, DateTimeOffset? until, CancellationToken cancellationToken) =>
        ReversalAttempts.RetriedAsync(dataSource, scope => DecideInAsync(scope, act, until, cancellationToken), time, cancellationToken);

    private static async Task<(ReversalResult, CustodyEvent?)> WriteAsync(CustodyScope scope, ReversalAct act, DateTimeOffset? until, CancellationToken cancellationToken)
    {
        if (until is null)
        {
            return (new ReversalResult(ReversalOutcome.Recorded) { Id = act.RequestId }, CustodyEvent.ReversalDeclined(act.PersonId, act.RequestId));
        }

        Bound[] row =
        [
            new("domain", NpgsqlDbType.Integer, act.DomainId), new("request", NpgsqlDbType.Bigint, act.RequestId), new("person", NpgsqlDbType.Bigint, act.PersonId),
            new("until", NpgsqlDbType.TimestampTz, until.Value.ToUniversalTime()),
        ];
        var id = (long)(await ScalarAsync(scope, (ContentQueries.InsertReversalAuthorization, row), cancellationToken).ConfigureAwait(false))!;
        return (new ReversalResult(ReversalOutcome.Recorded) { Id = id }, CustodyEvent.ReversalAuthorized(act.PersonId, id, act.RequestId));
    }

    private async Task<(ReversalResult, CustodyEvent?)> DecideInAsync(CustodyScope scope, ReversalAct act, DateTimeOffset? until, CancellationToken cancellationToken)
    {
        await DeclareAsync(scope, act.DomainId, cancellationToken).ConfigureAwait(false);
        var now = time.GetUtcNow();
        if (await ReversalStanding.ReadAsync(scope, act.RequestId, now, cancellationToken).ConfigureAwait(false) is not { } request)
        {
            return (new ReversalResult(ReversalOutcome.NotFound), null);
        }

        var ask = new ReversalAuthorizationAsk
        {
            RequestedBy = request.RequestedBy,
            AuthorizedBy = act.PersonId,
            AuthorizerIsCuration = await ReversalStanding.IsCurationAsync(scope, act.PersonId, cancellationToken).ConfigureAwait(false),
            ValidUntil = until,
            Now = now,
        };
        return ReversalAuthorizationRule.Judge(ask) switch
        {
            ReversalAuthorizationVerdict.NotCuration => Refused(ReversalOutcome.NotCuration, CustodyReason.ReversalFunction),
            ReversalAuthorizationVerdict.OwnRequest => Refused(ReversalOutcome.OwnRequest, CustodyReason.ReversalSelf),
            ReversalAuthorizationVerdict.TermInThePast => (new ReversalResult(ReversalOutcome.TermInThePast), null),
            ReversalAuthorizationVerdict.Authorized when request.State is ReversalState.Declined or ReversalState.Authorized or ReversalState.Revealed =>
                (new ReversalResult(ReversalOutcome.Closed), null),
            ReversalAuthorizationVerdict.Authorized => await WriteAsync(scope, act, until, cancellationToken).ConfigureAwait(false),
            _ => throw new UnreachableException(),
        };

        (ReversalResult, CustodyEvent?) Refused(ReversalOutcome outcome, CustodyReason reason) =>
            (new ReversalResult(outcome), CustodyEvent.ReversalAuthorizationRefused(act.PersonId, act.RequestId, reason));
    }
}
