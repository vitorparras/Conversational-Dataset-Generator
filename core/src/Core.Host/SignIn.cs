using System.Net;
using Core.Persistence.People;
using Core.Platform.Sessions;

namespace Core.Host;

/// <summary>
/// The one place where a name of use and a password become a session: the password is checked by the store of credentials and nowhere else,
/// so whatever else must happen at the entry hangs here. A name nobody goes by is checked all the same, against nobody, so that it costs what a wrong password costs.
/// A refusal waits the delay of <see cref="SignInDelay"/> before it is answered, the same for a wrong password and for a name nobody goes by; while the answers that wait
/// fill the limits, the entry is refused with the same answer before the password is checked, whatever the password.
/// </summary>
internal sealed class SignIn(PersonRegistry registry, PersonCredentials credentials, SessionStore sessions, SignInDelay delay)
{
    private const long Nobody = 0;

    /// <summary>The identifier of the session that was opened; null when the entry is refused, whatever the reason.</summary>
    internal async Task<string?> OpenAsync(string name, string password, IPAddress? origin, CancellationToken cancellationToken)
    {
        if (delay.Saturated(origin))
        {
            delay.Counted(name, origin);
            return null;
        }

        var identity = await registry.IdOfAsync(name, cancellationToken).ConfigureAwait(false);
        var outcome = await credentials.VerifyAsync(identity ?? Nobody, password, cancellationToken).ConfigureAwait(false);
        if (outcome == PasswordOutcome.Accepted && identity is { } admitted)
        {
            delay.Accepted(name, origin);
            return sessions.Open(admitted);
        }

        await delay.RefusedAsync(name, origin, cancellationToken).ConfigureAwait(false);
        return null;
    }
}
