using System.Net;
using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Platform.Sessions;

namespace Core.Host;

/// <summary>
/// The one place where a name of use and a password become a session: the password is checked by the store of credentials and nowhere else,
/// so whatever else must happen at the entry hangs here. A name nobody goes by is checked all the same, against nobody, so that it costs what a wrong password costs.
/// A refusal waits the delay of <see cref="SignInDelay"/> before it is answered, the same for a wrong password and for a name nobody goes by; while the answers that wait
/// fill the limits, the entry is refused with the same answer before the password is checked, whatever the password. Every attempt that reaches the check, accepted or refused,
/// is recorded in the chain (<see cref="SignInAttempts"/>) before it is answered, and a session opens only for an attempt that was recorded; the answer refused for the limit
/// reaches no database, and is not recorded. A right password whose acceptance meets another writer at the chain is recorded again, up to a limit; if the chain stays busy the entry is
/// not refused: it is told apart (<see cref="SignInEntry.ChainBusy"/>), counts nothing against the name nor the origin, waits no delay and writes nothing, because the password was not
/// wrong and no reason would be true. A right password whose acceptance the chain refused for any other reason is refused all the same, and as a name nobody goes by only when the person
/// was deactivated meanwhile.
/// </summary>
internal sealed class SignIn(PersonRegistry registry, PersonCredentials credentials, SessionStore sessions, SignInDelay delay, SignInRecord attempts)
{
    private const long Nobody = 0;

    /// <summary>Whether nobody is registered yet, which the entry says so that the person knows the step is on the server.</summary>
    internal Task<bool> NobodyRegisteredAsync(CancellationToken cancellationToken) => registry.IsEmptyAsync(cancellationToken);

    /// <summary>The identifier of the session that was opened; none when the entry is refused, whatever the reason, or when the chain stayed busy, which the result tells apart.</summary>
    internal async Task<SignInEntry> OpenAsync(string name, string password, IPAddress? origin, CancellationToken cancellationToken)
    {
        using var admission = delay.Admit(origin);
        if (admission is null)
        {
            delay.Counted(name, origin);
            return SignInEntry.Refused;
        }

        var identity = await registry.IdOfAsync(name, cancellationToken).ConfigureAwait(false);
        var outcome = await credentials.VerifyAsync(identity ?? Nobody, password, cancellationToken).ConfigureAwait(false);
        if (outcome == PasswordOutcome.Accepted && identity is { } admitted)
        {
            var accepted = await attempts.AcceptedAsync(admitted, origin, cancellationToken).ConfigureAwait(false);
            if (accepted.Outcome == CustodyOutcome.Appended)
            {
                delay.Accepted(name, origin);
                return new SignInEntry(sessions.Open(admitted), ChainBusy: false);
            }

            if (accepted.Outcome == CustodyOutcome.Conflict)
            {
                attempts.AcceptanceNotRecorded(accepted.Outcome, origin);
                return SignInEntry.Busy;
            }

            if (accepted.Outcome != CustodyOutcome.ActorDeactivated)
            {
                await delay.RefusedAsync(name, origin, cancellationToken).ConfigureAwait(false);
                return SignInEntry.Refused;
            }
        }

        var wrong = outcome == PasswordOutcome.Wrong;
        await attempts.RefusedAsync(wrong ? identity : null, wrong ? SignInRefusal.PasswordWrong : SignInRefusal.AccountNotRecognized, origin, cancellationToken).ConfigureAwait(false);
        await delay.RefusedAsync(name, origin, cancellationToken).ConfigureAwait(false);
        return SignInEntry.Refused;
    }
}
