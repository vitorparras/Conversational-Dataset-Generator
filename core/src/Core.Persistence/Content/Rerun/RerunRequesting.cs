using System.Runtime.CompilerServices;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Chain.Append;
using Npgsql;

namespace Core.Persistence.Content.Rerun;

/// <summary>
/// A request of a rerun, in one serializable transaction with its event: what was left of a request to another version is closed as superseded, and the items of another version are queued;
/// with none to queue, nothing but the superseding is written and the open request is answered.
/// </summary>
internal static class RerunRequesting
{
    private const int Attempts = 3;

    internal static async Task<RerunRequestResult> RequestAsync((NpgsqlDataSource DataSource, TimeProvider Time) around, RerunRequest request, CancellationToken cancellationToken)
    {
        var tries = 0;
        RerunRequestResult result;
        do
        {
            result = await RequestingAsync(around.DataSource, request, around.Time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        }
        while (result.Refusal?.Outcome == CustodyOutcome.Conflict && ++tries < Attempts);

        return result;
    }

    private static async Task<RerunRequestResult> RequestingAsync(NpgsqlDataSource dataSource, RerunRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var answer = new StrongBox<RerunRequestResult>();
        CustodyResult? recorded;
        try
        {
            recorded = await CustodyTransaction.RunAsync(dataSource, scope => RequestInAsync(scope, (request, now), answer, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException failed) when (failed.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // Another request queued the same item first; the next attempt finds it in the open request.
            recorded = CustodyRefusals.Conflict();
        }

        return recorded is null or { Outcome: CustodyOutcome.Appended } ? answer.Value! : new RerunRequestResult(RerunRequestOutcome.NotRecorded) { Refusal = recorded };
    }

    private static async Task<CustodyResult?> RequestInAsync(
        CustodyScope scope, (RerunRequest Request, DateTimeOffset Now) asked, StrongBox<RerunRequestResult> answer, CancellationToken cancellationToken)
    {
        var (request, now) = asked;
        await RerunCommands.DeclareAsync(scope, request.DomainId, cancellationToken).ConfigureAwait(false);
        var superseded = await RerunCommands.ConcludedAsync(scope, (ContentQueries.CloseRerunSuperseded, RerunCommands.Over(request.Version, now)), null, cancellationToken)
            .ConfigureAwait(false);
        var recorded = await RerunCommands.RecordEachAsync(scope, superseded, CustodyReason.SanitizationSuperseded, cancellationToken).ConfigureAwait(false);
        if (recorded is { Outcome: not CustodyOutcome.Appended })
        {
            return recorded;
        }

        if (!await RerunReading.PendingAsync(scope, request.Version, cancellationToken).ConfigureAwait(false))
        {
            var open = await RerunReading.OpenAsync(scope, cancellationToken).ConfigureAwait(false);
            answer.Value = open is null ? new RerunRequestResult(RerunRequestOutcome.NothingToRerun) : new RerunRequestResult(RerunRequestOutcome.AlreadyOpen, open.RerunId);
            return recorded is null ? null : await CustodyAppend.CommitIfAppendedAsync(scope, recorded, cancellationToken).ConfigureAwait(false);
        }

        var rerunId = await RerunReading.InsertAsync(scope, request, now, cancellationToken).ConfigureAwait(false);
        answer.Value = new RerunRequestResult(RerunRequestOutcome.Queued, rerunId);
        var appended = await CustodyAppend.AppendAsync(scope, CustodyEvent.SanitizationRerun(request.PersonId, rerunId, request.Version), cancellationToken).ConfigureAwait(false);
        return await CustodyAppend.CommitIfAppendedAsync(scope, appended, cancellationToken).ConfigureAwait(false);
    }
}
