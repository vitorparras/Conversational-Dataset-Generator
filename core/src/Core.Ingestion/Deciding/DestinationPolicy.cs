using System.Diagnostics;
using Core.Boundary.Contracts;

namespace Core.Ingestion.Deciding;

/// <summary>
/// What becomes of each occurrence, decided once, from the occurrence alone. A secret is always removed. A name is pseudonymized when it is in the function of a person and preserved when
/// it is in the function of a place; the part of speech says whether it is a name, and the syntactic role says the function when the kind alone does not (decision 0018).
/// Whatever the policy does not recognise is removed, so that a kind it never heard of cannot pass as clean.
/// </summary>
public static class DestinationPolicy
{
    private const string ProperNoun = "PROPN";

    /// <summary>The one destination of the occurrence.</summary>
    /// <param name="occurrence">What the service found, with its kind, part of speech and syntactic role.</param>
    public static Destination Of(Occurrence occurrence)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        var kind = Normalized(occurrence.Kind);
        if (IsSecret(kind))
        {
            return Destination.Remove;
        }

        var name = string.Equals(Normalized(occurrence.PartOfSpeech), ProperNoun, StringComparison.Ordinal);
        return FunctionOf(kind, occurrence.SyntacticRole) switch
        {
            Function.Person => name ? Destination.Pseudonymize : Destination.Remove,
            Function.Place => name ? Destination.Preserve : Destination.Remove,
            Function.Other => Destination.Remove,
            _ => throw new UnreachableException(),
        };
    }

    private static string Normalized(string text) => text.Trim().ToUpperInvariant();

    private static bool IsSecret(string kind) => kind switch
    {
        "CREDENTIAL" or "SECRET" or "PASSWORD" => true,
        "TOKEN" or "API_KEY" or "PRIVATE_KEY" => true,
        _ => false,
    };

    /// <summary>A place named in the role of someone who acts or is addressed is a person in that sentence; the kind decides the rest.</summary>
    private static Function FunctionOf(string kind, string role) => kind switch
    {
        "PERSON" or "ORGANIZATION" => Function.Person,
        "LOCATION" => TakesPart(role) ? Function.Person : Function.Place,
        _ => Function.Other,
    };

    /// <summary>The role with no subtype after the colon is one of the five in which a participant of the sentence stands.</summary>
    private static bool TakesPart(string role) => Normalized(role).Split(':')[0] switch
    {
        "NSUBJ" or "CSUBJ" or "OBJ" => true,
        "IOBJ" or "VOCATIVE" => true,
        _ => false,
    };

    private enum Function
    {
        Other,
        Person,
        Place,
    }
}
