namespace Core.Persistence.People;

/// <summary>The functions a person may hold, as the access policy declares them; the registry takes no other, and the list lives here only.</summary>
public static class PersonFunctions
{
    /// <summary>The function that empties an item of the content: the curation of the collection.</summary>
    public static string Curation { get; } = "Curadoria do acervo";

    /// <summary>The six functions of the policy, by the name the policy gives each.</summary>
    public static IReadOnlyList<string> Accepted { get; } =
    [
        "Anotação",
        "Arbitragem",
        Curation,
        "Avaliação",
        "Auditoria",
        "Administração técnica",
    ];

    /// <summary>Whether the name is one of the accepted, letter by letter.</summary>
    public static bool IsAccepted(string function) => Accepted.Contains(function, StringComparer.Ordinal);
}
