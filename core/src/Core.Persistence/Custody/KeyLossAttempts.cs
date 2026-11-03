using System.Collections.Frozen;

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

/// <summary>The words the operator writes for each thing that may have been tried, and the reading of a list of them.</summary>
public static class KeyLossAttemptNames
{
    private const char Separator = ',';

    private static readonly FrozenDictionary<string, KeyLossAttempts> Known = new Dictionary<string, KeyLossAttempts>
    {
        ["copia-de-guarda"] = KeyLossAttempts.SafekeepingCopy,
        ["copia-anterior-da-guarda"] = KeyLossAttempts.PreviousSafekeepingCopy,
        ["registro-de-criacao-do-dominio"] = KeyLossAttempts.DomainCreationRecord,
    }.ToFrozenDictionary();

    /// <summary>The words accepted, in the order they are told.</summary>
    public static IEnumerable<string> Accepted => Known.Keys;

    /// <summary>Reads a list of the words apart by commas; a word outside the list, a repeated one or an empty list is not read.</summary>
    /// <param name="text">The list as written.</param>
    /// <param name="attempts">What the list says, when it was read.</param>
    public static bool TryRead(string? text, out KeyLossAttempts attempts)
    {
        attempts = KeyLossAttempts.None;
        if (text is null)
        {
            return false;
        }

        foreach (var word in text.Split(Separator))
        {
            if (!Known.TryGetValue(word, out var one) || attempts.HasFlag(one))
            {
                attempts = KeyLossAttempts.None;
                return false;
            }

            attempts |= one;
        }

        return attempts != KeyLossAttempts.None;
    }
}
