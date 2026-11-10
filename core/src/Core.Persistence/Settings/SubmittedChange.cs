namespace Core.Persistence.Settings;

/// <summary>A change of configuration as it was submitted, with the event of its submission and whether it was decided already (decision 0053).</summary>
public sealed record SubmittedChange
{
    /// <summary>The submission.</summary>
    public required long Id { get; init; }

    /// <summary>The key, as <c>Section:Name</c>.</summary>
    public required string Key { get; init; }

    /// <summary>The new value.</summary>
    public required string Value { get; init; }

    /// <summary>The code of the motive.</summary>
    public required string Motive { get; init; }

    /// <summary>The person who submitted.</summary>
    public required long AuthorId { get; init; }

    /// <summary>When the server took the submission.</summary>
    public required DateTimeOffset SubmittedAt { get; init; }

    /// <summary>The sequence of the event of the submission, which the decision points at.</summary>
    public required long Sequence { get; init; }

    /// <summary>Whether someone approved or refused it already.</summary>
    public required bool Decided { get; init; }
}
