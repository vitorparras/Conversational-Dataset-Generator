namespace Core.Ingestion.Contracts.Reversal;

/// <summary>The judgment of a request to reverse a marker: the request accepted, the fields missing, or how far it reaches.</summary>
public sealed record ReversalRequestJudgment
{
    /// <summary>What the judgment comes to.</summary>
    public required ReversalRequestVerdict Verdict { get; init; }

    /// <summary>The request, only when <see cref="ReversalRequestVerdict.Accepted"/>.</summary>
    public ReversalRequest? Request { get; init; }

    /// <summary>The fields missing or not what they must be, in the order of the form, only when <see cref="ReversalRequestVerdict.Incomplete"/>.</summary>
    public IReadOnlyList<ReversalRequestField> Missing { get; init; } = [];

    /// <summary>How far the request reaches, only when <see cref="ReversalRequestVerdict.TooWide"/>.</summary>
    public ReversalScope? Scope { get; init; }
}
