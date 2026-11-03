using Core.Persistence.People;
using Npgsql;

namespace Core.Tests.Integration.Hosts;

/// <summary>
/// The verification the host makes when it comes up and the first write of a helper that starts a host and then writes to its database: both are serializable transactions on the chain,
/// so one that meets the other is refused with a conflict, and the helper's write would fail by chance. A helper waits here until the verification of the start is recorded in the chain, which
/// is what the route of the last verification shows, before it writes; the next verification is an interval away.
/// </summary>
internal static class StartupVerification
{
    private const string Recorded = "SELECT EXISTS (SELECT 1 FROM public.custody_event WHERE step = 'verify_chain')";
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(50);

    /// <summary>Returns when the chain holds the verification of the start; fails when it does not within the patience of the helpers.</summary>
    public static async Task WaitUntilRecordedAsync(NpgsqlDataSource application, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(Patience);
        try
        {
            while (!await IsRecordedAsync(application, limit.Token))
            {
                await Task.Delay(Poll, limit.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The verification of the start was not recorded in the chain within {Patience}.");
        }
    }

    /// <summary>
    /// A write of a helper, tried again while the chain answers that another write reached the same link first: a host with a short interval verifies again while the helper writes, and the
    /// refusal says the request can be repeated. Any other answer is returned as it is, and so is the busy one when the patience runs out.
    /// </summary>
    public static async Task<PersonResult> WriteAsync(Func<Task<PersonResult>> write, CancellationToken cancellationToken)
    {
        var until = DateTimeOffset.UtcNow + Patience;
        while (true)
        {
            var result = await write();
            if (result.Outcome != PersonOutcome.ChainBusy || DateTimeOffset.UtcNow > until)
            {
                return result;
            }

            await Task.Delay(Poll, cancellationToken);
        }
    }

    private static async Task<bool> IsRecordedAsync(NpgsqlDataSource application, CancellationToken cancellationToken)
    {
        await using var command = application.CreateCommand(Recorded);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
