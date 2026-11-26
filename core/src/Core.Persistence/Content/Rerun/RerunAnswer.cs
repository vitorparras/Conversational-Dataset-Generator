namespace Core.Persistence.Content.Rerun;

/// <summary>What the domain made of an item rerun: the new texts, the refusal and its reason, or neither, when it could not answer now and the item spends a try.</summary>
/// <param name="Produced">The new texts; null when the item is refused or unanswered.</param>
/// <param name="Refusal">Why the item is refused; null when it is not.</param>
public sealed record RerunAnswer(Resanitized? Produced, RerunRefusal? Refusal)
{
    /// <summary>No answer now: the item spends a try and goes back to the queue.</summary>
    public static RerunAnswer Unanswered { get; } = new(null, null);

    /// <summary>The texts produced, or the refusal of an item whose destinations were never decided when there are none.</summary>
    public static RerunAnswer Of(Resanitized? produced) => new(produced, produced is null ? RerunRefusal.Undecided : null);
}
