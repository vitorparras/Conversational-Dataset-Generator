using System.Globalization;

namespace Core.Persistence.Custody;

/// <summary>
/// The reference of the events that open and close the window of the owner credential: a line of lowercase letters, digits, = and ; only, as the database requires (migration 0018),
/// with what the window is for and the tip of the chain it found when it opens, and the events it points at when it closes.
/// </summary>
internal static class OwnerWindowReference
{
    /// <summary>The reference of an opening: the versions it leaves and reaches, the tip it found (zero for none) and whether it was written after the step that creates it.</summary>
    internal static string Opened(int from, int to, ChainTip tip, bool afterTheStep) =>
        string.Create(CultureInfo.InvariantCulture, $"from={from};to={to};tip={tip.Sequence}{Digest(tip)}{(afterTheStep ? ";late=1" : string.Empty)}");

    /// <summary>The reference of a closing: the event that opened the window, the verification of the chain and the conference of the guarantees that came before it.</summary>
    internal static string Closed(long opened, long chain, long conference) =>
        string.Create(CultureInfo.InvariantCulture, $"opened={opened};chain={chain};conference={conference}");

    /// <summary>The fields of a reference, by name; a piece that is not a name and a value is left out.</summary>
    internal static IDictionary<string, string> Fields(string reference) =>
        reference.Split(';').Select(piece => piece.Split('=')).Where(pair => pair.Length == 2).ToDictionary(pair => pair[0], pair => pair[1], StringComparer.Ordinal);

    private static string Digest(ChainTip tip) => tip.Digest is null ? string.Empty : $";digest={Convert.ToHexStringLower(tip.Digest)}";
}
