namespace Core.Persistence.Ingestion;

/// <summary>
/// The conditions under which material may come in, from the most restrictive to the most permissive: the position in the list is the order of restriction, and nothing else gives it.
/// </summary>
public sealed class OriginConditions
{
    private const int LongestName = 63;

    private readonly string[] names;

    private OriginConditions(string[] names)
    {
        this.names = names;
    }

    /// <summary>The names, the most restrictive first.</summary>
    public IReadOnlyList<string> Names => names;

    /// <summary>The condition a batch takes when its opening declares none, and never the most permissive.</summary>
    public string MostRestrictive => names[0];

    /// <summary>The conditions in the order given, which must be a list with no repeated name and only names.</summary>
    /// <param name="ordered">The names, the most restrictive first.</param>
    public static OriginConditions Of(IReadOnlyList<string> ordered)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        return Refusal(ordered) is { } reason ? throw new ArgumentException(reason, nameof(ordered)) : new OriginConditions([.. ordered]);
    }

    /// <summary>Why the list is not one the installation can run with; null when it is.</summary>
    /// <param name="ordered">The names, the most restrictive first.</param>
    public static string? Refusal(IReadOnlyList<string> ordered)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        if (ordered.Count == 0)
        {
            return "a lista de condições está vazia";
        }

        if (ordered.Any(name => !IsName(name)))
        {
            return "uma condição não é um nome de letras minúsculas, algarismos e sublinhado";
        }

        return ordered.Distinct(StringComparer.Ordinal).Count() == ordered.Count ? null : "uma condição se repete";
    }

    /// <summary>Whether the text is a name of a condition: a lowercase letter, then lowercase letters, digits and underscores, up to 63 characters, as the database holds it.</summary>
    /// <param name="candidate">The text.</param>
    public static bool IsName(string? candidate) =>
        candidate is { Length: > 0 and <= LongestName } && char.IsAsciiLetterLower(candidate[0]) && candidate.All(Allowed);

    private static bool Allowed(char character) => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '_';

    /// <summary>The place of the condition in the order, 0 for the most restrictive; -1 when the installation does not know it.</summary>
    /// <param name="name">The condition.</param>
    public int RankOf(string name) => Array.IndexOf(names, name);
}
