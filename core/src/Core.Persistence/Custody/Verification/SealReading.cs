namespace Core.Persistence.Custody.Verification;

/// <summary>What reading the text of a seal gave: the seal, or the problem and its words for whoever operates.</summary>
public sealed record SealReading
{
    /// <summary>The seal the text holds; null when it was not read.</summary>
    public ChainSeal? Seal { get; init; }

    /// <summary>Why the text was not read; null when it was.</summary>
    public SealProblem? Problem { get; init; }

    /// <summary>The reason in words, for whoever operates; null when the text was read.</summary>
    public string? Refusal { get; init; }

    internal static SealReading Of(ChainSeal seal) => new() { Seal = seal };

    internal static SealReading Refused(SealProblem problem) => new() { Problem = problem, Refusal = SealWords.Of(problem) };
}
