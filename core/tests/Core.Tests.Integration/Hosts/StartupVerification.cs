using System.Net;
using Core.Persistence.People;
using Npgsql;

namespace Core.Tests.Integration.Hosts;

/// <summary>
/// The round the host makes when it comes up and the first write of a helper that starts a host and then writes to its database: the round writes twice to the chain, the verification of the
/// chain and then the conference of the guarantees, and every write is a serializable transaction, so one that meets another is refused with a conflict. The helper's write would fail by chance,
/// and so would the host's: the conference that loses is not repeated before the next interval, which by default is minutes away, and the route keeps saying no guarantee was conferred.
/// A helper waits here until the whole round is recorded in the chain, which is what the route of the last verification and the route of the guarantees show, before it writes.
/// </summary>
internal static class StartupVerification
{
    private const string Recorded = "SELECT count(DISTINCT step) = 2 FROM public.custody_event WHERE step IN ('verify_chain', 'verify_guarantees')";
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(50);

    /// <summary>Returns when the chain holds the verification and the conference of the start; fails when it does not within the patience of the helpers.</summary>
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
            throw new TimeoutException($"The verification and the conference of the start were not recorded in the chain within {Patience}.");
        }
    }

    /// <summary>
    /// The entry of a helper with a password it knows is right, sent again while the host refuses it: the entry is recorded in the chain before it is answered, so a host with a short interval
    /// whose round meets it refuses a right password. Any other answer is returned as it is, and so is the refusal when the patience runs out.
    /// </summary>
    public static async Task<HttpResponseMessage> EnterAsync(Func<Task<HttpResponseMessage>> enter, CancellationToken cancellationToken)
    {
        var until = DateTimeOffset.UtcNow + Patience;
        while (true)
        {
            var response = await enter();
            if (response.StatusCode != HttpStatusCode.Unauthorized || DateTimeOffset.UtcNow > until)
            {
                return response;
            }

            response.Dispose();
            await Task.Delay(Poll, cancellationToken);
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
