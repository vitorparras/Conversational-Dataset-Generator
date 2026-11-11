namespace Core.Boundary.Contracts;

/// <summary>
/// The kinds an occurrence may have, a closed list the contract fixes: a kind outside it is not an occurrence the core knows, and the answer that carries one is refused whole.
/// A secret is any of six named only in the list of secrets, and the destination of a secret is the same whatever the sentence says.
/// </summary>
public static class OccurrenceKinds
{
    /// <summary>A person.</summary>
    public static string Person => "PERSON";

    /// <summary>A place.</summary>
    public static string Location => "LOCATION";

    /// <summary>An organization.</summary>
    public static string Organization => "ORGANIZATION";

    /// <summary>Whether the kind is one of the list, written exactly as the list writes it.</summary>
    /// <param name="kind">The kind an occurrence carries.</param>
    public static bool IsKnown(string kind) => Names().Contains(kind) || Secrets().Contains(kind);

    /// <summary>Whether the kind is a credential or a secret, which is removed always.</summary>
    /// <param name="kind">The kind an occurrence carries.</param>
    public static bool IsSecret(string kind) => Secrets().Contains(kind);

    // Built at each call, and not once in a static initializer: a value fixed before any test runs is one the mutation of the text cannot reach.
    private static HashSet<string> Secrets() => new(StringComparer.Ordinal) { "CREDENTIAL", "SECRET", "PASSWORD", "TOKEN", "API_KEY", "PRIVATE_KEY" };

    private static HashSet<string> Names() => new(StringComparer.Ordinal) { "PERSON", "LOCATION", "ORGANIZATION" };
}
