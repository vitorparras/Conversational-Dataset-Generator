using System.Globalization;

namespace Core.Persistence.Custody;

public sealed partial record CustodyEvent
{
    /// <summary>
    /// The event of a person who changed their own password: they are the author and the item, and the reference is how many of their other sessions the change ended, in digits.
    /// It holds the act and never any value of a password.
    /// </summary>
    /// <param name="personId">The person who changed their password.</param>
    /// <param name="sessionsEnded">How many other sessions of theirs the change ended; none is zero.</param>
    public static CustodyEvent PasswordChanged(long personId, int sessionsEnded)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(personId);
        ArgumentOutOfRangeException.ThrowIfNegative(sessionsEnded);
        return new CustodyEvent(CustodyAuthor.Person(personId), new CustodyItem(CustodyItemKind.Person, personId), CustodyStep.PasswordChanged, CustodyReason.AdministrativeAct)
        {
            Reference = sessionsEnded.ToString(CultureInfo.InvariantCulture),
        };
    }
}
