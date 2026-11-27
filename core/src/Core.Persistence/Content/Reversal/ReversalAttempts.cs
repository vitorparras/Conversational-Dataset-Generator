using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Chain.Append;
using Npgsql;

namespace Core.Persistence.Content.Reversal;

/// <summary>Runs an act on a request in a serializable transaction with its event, again after a pause while it conflicts with another, up to three times.</summary>
internal static class ReversalAttempts
{
    /// <summary>How many times an act that conflicts with another is run, the first included.</summary>
    internal const int Attempts = 3;

    /// <summary>
    /// The wait before an act that conflicted runs again: the other writer may be finishing its commit, and while it is not yet seen a try at once meets the same conflict.
    /// </summary>
    internal static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(200);

    /// <summary>Runs the act until it is not a conflict with another, up to three times, pausing on the clock given between tries.</summary>
    internal static async Task<ReversalResult> RetriedAsync(
        NpgsqlDataSource dataSource, Func<CustodyScope, Task<(ReversalResult, CustodyEvent?)>> act, TimeProvider time, CancellationToken cancellationToken)
    {
        var tries = 0;
        while (true)
        {
            var result = await OnceAsync(dataSource, act, cancellationToken).ConfigureAwait(false);
            if (result.Refusal?.Outcome != CustodyOutcome.Conflict || ++tries >= Attempts)
            {
                return result;
            }

            await Task.Delay(Pause, time, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Runs the act and records the event it asks for, which commits it; an act that asks for none writes nothing.</summary>
    private static async Task<ReversalResult> OnceAsync(
        NpgsqlDataSource dataSource, Func<CustodyScope, Task<(ReversalResult, CustodyEvent?)>> act, CancellationToken cancellationToken)
    {
        var answer = new ReversalResult(ReversalOutcome.NotRecorded);
        try
        {
            var recorded = await CustodyTransaction.RunAsync(dataSource, async scope =>
            {
                (answer, var pending) = await act(scope).ConfigureAwait(false);
                return pending is null
                    ? null
                    : await CustodyAppend.CommitIfAppendedAsync(scope, await CustodyAppend.AppendAsync(scope, pending, cancellationToken).ConfigureAwait(false), cancellationToken)
                        .ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
            return recorded is null or { Outcome: CustodyOutcome.Appended } ? answer : new ReversalResult(ReversalOutcome.NotRecorded) { Refusal = recorded };
        }
        catch (PostgresException failed) when (failed.SqlState is CustodyStatements.ChainBrokenSqlState or CustodyStatements.ReversalSqlState)
        {
            // The database refuses a row with the chain broken, and an authorization that another, committed first, closed.
            return failed.SqlState == CustodyStatements.ReversalSqlState
                ? new ReversalResult(ReversalOutcome.Closed)
                : new ReversalResult(ReversalOutcome.NotRecorded) { Refusal = CustodyRefusals.ChainBroken() };
        }
    }
}
