namespace Core.Ingestion.Contracts.Reversal;

/// <summary>Someone who means to authorize a request to reverse a marker, and until when, with who asked for it and the moment it happens.</summary>
public sealed record ReversalAuthorizationAsk
{
    /// <summary>Who asked for the reversal.</summary>
    public required long RequestedBy { get; init; }

    /// <summary>Who means to authorize it.</summary>
    public required long AuthorizedBy { get; init; }

    /// <summary>Whether who means to authorize holds the function of the curation.</summary>
    public required bool AuthorizerIsCuration { get; init; }

    /// <summary>Until when the authorization would hold.</summary>
    public required DateTimeOffset ValidUntil { get; init; }

    /// <summary>The moment of the authorization.</summary>
    public required DateTimeOffset Now { get; init; }
}
