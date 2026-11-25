namespace Core.Ingestion.Contracts.Reversal;

/// <summary>
/// Who may authorize a request to reverse a marker, and until when (requirement 007): a person of the curation of the domain who is not who asked, so that no reversal rests on one
/// person alone, until a moment that has not come yet. The function is looked at first, then who asked, then the term.
/// </summary>
public static class ReversalAuthorizationRule
{
    /// <summary>The judgment of the authorization.</summary>
    /// <param name="ask">Who asked, who authorizes and whether they hold the curation, until when, and now.</param>
    public static ReversalAuthorizationVerdict Judge(ReversalAuthorizationAsk ask)
    {
        ArgumentNullException.ThrowIfNull(ask);
        if (!ask.AuthorizerIsCuration)
        {
            return ReversalAuthorizationVerdict.NotCuration;
        }
        if (ask.AuthorizedBy == ask.RequestedBy)
        {
            return ReversalAuthorizationVerdict.OwnRequest;
        }
        return ask.ValidUntil > ask.Now ? ReversalAuthorizationVerdict.Authorized : ReversalAuthorizationVerdict.TermInThePast;
    }
}
