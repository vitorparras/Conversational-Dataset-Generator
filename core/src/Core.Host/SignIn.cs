using Core.Persistence.People;
using Core.Platform.Sessions;

namespace Core.Host;

/// <summary>
/// The one place where a name of use and a password become a session: the password is checked by the store of credentials and nowhere else,
/// so whatever else must happen at the entry hangs here. A name nobody goes by is checked all the same, against nobody, so that it costs what a wrong password costs.
/// </summary>
internal sealed class SignIn(PersonRegistry registry, PersonCredentials credentials, SessionStore sessions)
{
    private const long Nobody = 0;

    /// <summary>The identifier of the session that was opened; null when the entry is refused, whatever the reason.</summary>
    internal async Task<string?> OpenAsync(string name, string password, CancellationToken cancellationToken)
    {
        var identity = await registry.IdOfAsync(name, cancellationToken).ConfigureAwait(false);
        var outcome = await credentials.VerifyAsync(identity ?? Nobody, password, cancellationToken).ConfigureAwait(false);
        return outcome == PasswordOutcome.Accepted && identity is { } admitted ? sessions.Open(admitted) : null;
    }
}
