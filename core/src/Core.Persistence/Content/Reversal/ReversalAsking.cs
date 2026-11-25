using Core.Ingestion.Contracts.Reversal;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;
using static Core.Persistence.Content.Rerun.RerunRows;

namespace Core.Persistence.Content.Reversal;

/// <summary>Asks for the reversal of one marker in one item: refused by scope, by function or by the markers of the item, each with its event, or sealed and written.</summary>
internal sealed class ReversalAsking(NpgsqlDataSource dataSource, ProtectedContent protectedContent)
{
    internal Task<ReversalResult> AskAsync((int Domain, long Person) by, ReversalRequestDraft draft, CancellationToken cancellationToken)
    {
        var judged = ReversalRequestRule.Judge(draft);
        return judged.Verdict == ReversalRequestVerdict.Incomplete
            ? Task.FromResult(new ReversalResult(ReversalOutcome.Incomplete) { Missing = judged.Missing })
            : ReversalAttempts.RetriedAsync(dataSource, scope => AskInAsync(scope, by, judged, cancellationToken), cancellationToken);
    }

    private async Task<(ReversalResult, CustodyEvent?)> AskInAsync(CustodyScope scope, (int Domain, long Person) by, ReversalRequestJudgment judged, CancellationToken cancellationToken)
    {
        await DeclareAsync(scope, by.Domain, cancellationToken).ConfigureAwait(false);
        if (judged.Request is not { } request)
        {
            var wide = judged.Scope!.Value;
            return (new ReversalResult(ReversalOutcome.TooWide) { Scope = wide }, CustodyEvent.ReversalRequestRefusedByScope(by.Person, by.Domain, wide));
        }

        if (!await ReversalStanding.IsCurationAsync(scope, by.Person, cancellationToken).ConfigureAwait(false))
        {
            return (new ReversalResult(ReversalOutcome.NotCuration), CustodyEvent.ReversalRequestRefused(by.Person, request.ItemId, request.Marker, CustodyReason.ReversalFunction));
        }

        Bound[] marker = [new("item", NpgsqlDbType.Bigint, request.ItemId), new("marker", NpgsqlDbType.Text, request.Marker)];
        if (await ScalarAsync(scope, (ContentQueries.ReadReversalMarkerInItem, marker), cancellationToken).ConfigureAwait(false) is null)
        {
            return (new ReversalResult(ReversalOutcome.MarkerNotInItem), CustodyEvent.ReversalRequestRefused(by.Person, request.ItemId, request.Marker, CustodyReason.ReversalScope));
        }

        var id = await InsertAsync(scope, by, request, cancellationToken).ConfigureAwait(false);
        return (new ReversalResult(ReversalOutcome.Recorded) { Id = id }, CustodyEvent.ReversalRequested(by.Person, id, request.Marker));
    }

    private async Task<long> InsertAsync(CustodyScope scope, (int Domain, long Person) by, ReversalRequest request, CancellationToken cancellationToken)
    {
        using var sealing = protectedContent.Begin(ContentColumns.ReversalRequestKey, by.Domain);
        var justification = sealing.Seal(ContentColumns.ReversalRequestText, ReversalStanding.Sealable(request));
        Bound[] row =
        [
            new("domain", NpgsqlDbType.Integer, by.Domain), new("item", NpgsqlDbType.Bigint, request.ItemId), new("marker", NpgsqlDbType.Text, request.Marker),
            new("person", NpgsqlDbType.Bigint, by.Person), new("term", NpgsqlDbType.Integer, request.TermDays),
            new("justification", NpgsqlDbType.Bytea, justification.Envelope.ToArray()), new("epoch", NpgsqlDbType.Integer, sealing.Stored.Epoch),
            new("wrapped", NpgsqlDbType.Bytea, sealing.Stored.Wrapped.ToArray()),
        ];
        return (long)(await ScalarAsync(scope, (ContentQueries.InsertReversalRequest, row), cancellationToken).ConfigureAwait(false))!;
    }
}
