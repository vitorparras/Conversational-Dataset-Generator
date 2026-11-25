namespace Core.Persistence.Content.Reversal;

/// <summary>Someone who acts on a request of the domain: to authorize, to decline or to read what it says.</summary>
/// <param name="DomainId">The domain of the request.</param>
/// <param name="RequestId">The request.</param>
/// <param name="PersonId">Who acts.</param>
public sealed record ReversalAct(int DomainId, long RequestId, long PersonId);
