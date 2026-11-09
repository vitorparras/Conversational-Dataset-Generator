namespace Core.Boundary.Contracts;

/// <summary>
/// The kinds an occurrence may have, a closed list the contract fixes: a kind outside it is not an occurrence the core knows, and the answer that carries one is refused whole.
/// A secret is any of the last six, and the destination of a secret is the same whatever the sentence says.
/// </summary>
public static class OccurrenceKinds
{
    private static readonly HashSet<string> Secrets = new(StringComparer.Ordinal) { "CREDENTIAL", "SECRET", "PASSWORD", "TOKEN", "API_KEY", "PRIVATE_KEY" };

    private static readonly HashSet<string> Names = new(StringComparer.Ordinal) { "PERSON", "LOCATION", "ORGANIZATION" };

    /// <summary>A person.</summary>
    public static string Person { get; } = "PERSON";

    /// <summary>A place.</summary>
    public static string Location { get; } = "LOCATION";

    /// <summary>An organization.</summary>
    public static string Organization { get; } = "ORGANIZATION";

    /// <summary>A credential.</summary>
    public static string Credential { get; } = "CREDENTIAL";

    /// <summary>A secret that is none of the others.</summary>
    public static string Secret { get; } = "SECRET";

    /// <summary>A password.</summary>
    public static string Password { get; } = "PASSWORD";

    /// <summary>A token of access.</summary>
    public static string Token { get; } = "TOKEN";

    /// <summary>A key of an interface.</summary>
    public static string ApiKey { get; } = "API_KEY";

    /// <summary>A private key.</summary>
    public static string PrivateKey { get; } = "PRIVATE_KEY";

    /// <summary>Whether the kind is one of the list, written exactly as the list writes it.</summary>
    /// <param name="kind">The kind an occurrence carries.</param>
    public static bool IsKnown(string kind) => Names.Contains(kind) || Secrets.Contains(kind);

    /// <summary>Whether the kind is a credential or a secret, which is removed always.</summary>
    /// <param name="kind">The kind an occurrence carries.</param>
    public static bool IsSecret(string kind) => Secrets.Contains(kind);
}
