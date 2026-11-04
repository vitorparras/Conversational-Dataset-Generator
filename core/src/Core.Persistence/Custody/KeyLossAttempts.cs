namespace Core.Persistence.Custody;

/// <summary>
/// What was tried to recover a key before its loss was declared: a closed list, so that nothing a caller writes reaches the chain.
/// The set is kept in the event as the sum of the values.
/// </summary>
[Flags]
public enum KeyLossAttempts
{
    /// <summary>Nothing was tried; the loss cannot be declared so.</summary>
    None = 0,

    /// <summary>The safekeeping copy of the key was looked for.</summary>
    SafekeepingCopy = 1,

    /// <summary>The copy before the last one of the safekeeping was looked for.</summary>
    PreviousSafekeepingCopy = 2,

    /// <summary>The record made when the domain was created was looked for.</summary>
    DomainCreationRecord = 4,

    /// <summary>Every thing that may be tried.</summary>
    All = SafekeepingCopy | PreviousSafekeepingCopy | DomainCreationRecord,
}
