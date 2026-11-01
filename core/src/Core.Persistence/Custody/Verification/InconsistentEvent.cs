namespace Core.Persistence.Custody.Verification;

/// <summary>The first event of the chain that does not hold, and how: the sequence it holds and nothing of what it records.</summary>
/// <param name="Sequence">The sequence of the event as the table holds it.</param>
/// <param name="Kind">How the event does not hold.</param>
public sealed record InconsistentEvent(long Sequence, ChainInconsistency Kind);
