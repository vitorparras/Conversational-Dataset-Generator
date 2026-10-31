namespace Core.Persistence.People;

/// <summary>The answer of an operation over a person: how it ended, whom it concerns when it was done, and the reason in words when it was refused.</summary>
public sealed record PersonResult
{
    /// <summary>How the operation ended.</summary>
    public required PersonOutcome Outcome { get; init; }

    /// <summary>The identifier of the person written or changed; null when the operation was refused before there was one.</summary>
    public long? PersonId { get; init; }

    /// <summary>The reason of the refusal, for whoever operates; null when the operation was done.</summary>
    public string? Refusal { get; init; }

    internal static PersonResult Done(long personId) => new() { Outcome = PersonOutcome.Done, PersonId = personId };

    internal static PersonResult Refused(PersonOutcome outcome, string refusal) => new() { Outcome = outcome, Refusal = refusal };
}
