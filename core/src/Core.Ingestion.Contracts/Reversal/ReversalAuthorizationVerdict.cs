namespace Core.Ingestion.Contracts.Reversal;

/// <summary>What the judgment of an authorization of a reversal comes to.</summary>
public enum ReversalAuthorizationVerdict
{
    /// <summary>Another person of the curation, until a moment to come: it may be authorized.</summary>
    Authorized = 0,

    /// <summary>Who means to authorize does not hold the function of the curation: refused, and the refusal is recorded.</summary>
    NotCuration = 1,

    /// <summary>Who means to authorize is who asked: refused, and the refusal is recorded.</summary>
    OwnRequest = 2,

    /// <summary>The moment until which it would hold is not to come: nothing is authorized and nothing is recorded.</summary>
    TermInThePast = 3,
}
