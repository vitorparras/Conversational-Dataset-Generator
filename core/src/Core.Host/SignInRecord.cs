using System.Net;
using Core.Persistence.Custody;
using Core.Persistence.People;

namespace Core.Host;

/// <summary>
/// The record of the entry as the host keeps it: what <see cref="SignInAttempts"/> writes to the chain, and a line of the log for a refusal
/// that did not reach the chain, so the series of the prefix does not lose it unseen. The line carries how the chain answered and the prefix
/// of the origin, and nothing the person typed.
/// </summary>
internal sealed class SignInRecord
{
    private readonly SignInAttempts attempts;
    private readonly ILoggerFactory logs;

    internal SignInRecord(SignInAttempts attempts, ILoggerFactory logs)
    {
        this.attempts = attempts;
        this.logs = logs;
    }

    /// <summary>Records an attempt that was accepted; anything but <see cref="CustodyOutcome.Appended"/> means it was not.</summary>
    internal Task<CustodyResult> AcceptedAsync(long author, IPAddress? origin, CancellationToken cancellationToken) =>
        attempts.AcceptedAsync(author, origin, cancellationToken);

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
