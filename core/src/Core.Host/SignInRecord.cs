using System.Net;
using Core.Persistence.Custody;
using Core.Persistence.People;

namespace Core.Host;

/// <summary>
/// The record of the entry as the host keeps it: what <see cref="SignInAttempts"/> writes to the chain, and a line of the log for a refusal
/// that did not reach the chain, so the series of the prefix does not lose it unseen. The line carries the reason and the prefix of the origin
/// and never the password. The writer of the refusals is given, so that a test can say how the chain answered.
/// </summary>
internal sealed class SignInRecord(
    SignInAttempts attempts, Func<long?, SignInRefusal, IPAddress?, CancellationToken, Task<CustodyResult>> refuse, ILogger<SignInRecord> logger)
{
    /// <summary>The record over the attempts as they write to the chain.</summary>
    internal static SignInRecord Over(SignInAttempts attempts, ILogger<SignInRecord> logger) => new(attempts, attempts.RefusedAsync, logger);

    /// <summary>Records an attempt that was accepted; anything but <see cref="CustodyOutcome.Appended"/> means it was not.</summary>
    internal Task<CustodyResult> AcceptedAsync(long personId, IPAddress? origin, CancellationToken cancellationToken) =>
        attempts.AcceptedAsync(personId, origin, cancellationToken);

    /// <summary>Records an attempt that was refused; one that does not reach the chain leaves a line of the log with the reason and the prefix.</summary>
    internal async Task RefusedAsync(long? personId, SignInRefusal refusal, IPAddress? origin, CancellationToken cancellationToken)
    {
        var result = await refuse(personId, refusal, origin, cancellationToken).ConfigureAwait(false);
        if (result.Outcome != CustodyOutcome.Appended)
        {
            logger.RefusalNotRecorded(refusal, result.Outcome, SignInAttempts.NetworkOf(origin) ?? "desconhecido");
        }
    }
}
