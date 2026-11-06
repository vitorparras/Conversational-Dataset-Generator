using System.Globalization;
using Core.Persistence.People;
using Core.Platform.Sessions;

namespace Core.Host.Operators;

/// <summary>
/// The change of the own password (decision 0029): the current one is checked by the store of credentials, and a wrong one is refused with the same answer as any other cause and counted in the
/// delay of the entry (<see cref="SignInDelay"/>), by the origin it came from and by the account, since the name of use is not asked here; a full limit of the entry is refused the same way. The
/// new password is derived with the work factor in force. A change that is written ends the other sessions of the account and keeps the one that asked; the event records who, when and how many
/// sessions were open at the moment of the write, and never a password.
/// </summary>
internal sealed class PasswordChange(PersonCredentials credentials, SessionStore sessions, SignInDelay delay)
{
    /// <summary>The result of the change asked by the account of a session, with the password it knows and the one it wants.</summary>
    internal async Task<PasswordChangeResult> ChangeAsync(PasswordChangeRequest who, string current, string replacement, CancellationToken cancellationToken)
    {
        if (replacement.Length == 0)
        {
            return new PasswordChangeResult(PasswordChangeOutcome.EmptyReplacement);
        }

        var counted = string.Create(CultureInfo.InvariantCulture, $"account:{who.Account}");
        using var admission = delay.Admit(who.Origin);
        if (admission is null)
        {
            delay.Counted(counted, who.Origin);
            return new PasswordChangeResult(PasswordChangeOutcome.Refused);
        }

        if (await credentials.VerifyAsync(who.Account, current, cancellationToken).ConfigureAwait(false) != PasswordOutcome.Accepted)
        {
            await delay.RefusedAsync(counted, who.Origin, cancellationToken).ConfigureAwait(false);
            return new PasswordChangeResult(PasswordChangeOutcome.Refused);
        }

        var others = sessions.ListOf(who.Account, who.Session).Count(session => !session.Current);
        var written = await credentials.ChangeOwnPasswordAsync(who.Account, replacement, others, cancellationToken).ConfigureAwait(false);
        return written.Outcome == PersonOutcome.Done
            ? new PasswordChangeResult(PasswordChangeOutcome.Changed, sessions.CloseOthersOf(who.Account, who.Session))
            : new PasswordChangeResult(PasswordChangeOutcome.NotWritten, Failure: written);
    }
}
