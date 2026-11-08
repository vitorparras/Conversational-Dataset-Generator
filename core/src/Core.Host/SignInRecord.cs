using System.Net;
using Core.Persistence.Custody;
using Core.Persistence.People;

namespace Core.Host;

/// <summary>
/// The record of the entry as the host keeps it: what <see cref="SignInAttempts"/> writes to the chain, and a line of the log for a refusal
/// that did not reach the chain, so the series of the prefix does not lose it unseen. The line carries how the chain answered and the prefix
/// of the origin, and nothing the person typed. An attempt accepted that meets another writer at the chain even after the tries of <see cref="SignInAttempts"/>
/// is recorded again after a short pause, a few rounds more, because refusing a right password for it would say what is not true.
/// </summary>
internal sealed class SignInRecord
{
    private const int Rounds = 4;
    private static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(250);

    private readonly SignInAttempts attempts;
    private readonly ILoggerFactory logs;

    internal SignInRecord(SignInAttempts attempts, ILoggerFactory logs)
    {
        this.attempts = attempts;
        this.logs = logs;
    }

    /// <summary>Records an attempt that was accepted, again while the chain is busy up to the rounds given; anything but <see cref="CustodyOutcome.Appended"/> means it was not.</summary>
    internal async Task<CustodyResult> AcceptedAsync(long author, IPAddress? origin, CancellationToken cancellationToken)
    {
        var round = 0;
        while (true)
        {
            var result = await attempts.AcceptedAsync(author, origin, cancellationToken).ConfigureAwait(false);
            if (result.Outcome != CustodyOutcome.Conflict || ++round >= Rounds)
            {
                return result;
            }

            await Task.Delay(Pause, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Leaves a line of the log for an accepted attempt the chain stayed too busy to take, with how it answered and the prefix of the origin.</summary>
    internal void AcceptanceNotRecorded(CustodyOutcome outcome, IPAddress? origin) =>
        logs.CreateLogger<SignInRecord>().AcceptanceNotRecorded(outcome, SignInAttempts.NetworkOf(origin) ?? "desconhecido");

    /// <summary>Records an attempt that was refused; one that does not reach the chain leaves a line of the log with how the chain answered and the prefix.</summary>
    internal async Task RefusedAsync(long? author, SignInRefusal refusal, IPAddress? origin, CancellationToken cancellationToken)
    {
        var result = await attempts.RefusedAsync(author, refusal, origin, cancellationToken).ConfigureAwait(false);
        if (result.Outcome != CustodyOutcome.Appended)
        {
            logs.CreateLogger<SignInRecord>().RefusalNotRecorded(result.Outcome, SignInAttempts.NetworkOf(origin) ?? "desconhecido");
        }
    }
}
