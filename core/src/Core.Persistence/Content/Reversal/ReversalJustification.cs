using Core.Persistence.Content.Reading;

namespace Core.Persistence.Content.Reversal;

/// <summary>Why the request asks for the identity and what will be done with it, opened only after the chain recorded the reading.</summary>
/// <param name="Outcome">What became of the reading.</param>
/// <param name="Reason">Why the analysis does not go on without the identity.</param>
/// <param name="Use">What will be done with it.</param>
public sealed record ReversalJustification(ContentReadOutcome Outcome, string? Reason = null, string? Use = null);
