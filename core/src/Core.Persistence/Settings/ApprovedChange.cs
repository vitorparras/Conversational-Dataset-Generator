namespace Core.Persistence.Settings;

/// <summary>
/// A change of configuration approved on the screen, as the applier reads it from the database (decision 0053): what was submitted, who submitted and who approved, and the sequences of the
/// two events of the chain that the applier checks before it touches the file.
/// </summary>
public sealed record ApprovedChange
{
    /// <summary>The submission.</summary>
    internal required long Id { get; init; }

    /// <summary>The key, as <c>Section:Name</c>.</summary>
    internal required string Key { get; init; }

    /// <summary>The new value.</summary>
    internal required string Value { get; init; }

    /// <summary>The person who submitted.</summary>
    internal required long SubmitterId { get; init; }

    /// <summary>The sequence of the event of the submission.</summary>
    internal required long SubmissionSequence { get; init; }

    /// <summary>The person who approved.</summary>
    internal required long ApproverId { get; init; }

    /// <summary>The sequence of the event of the approval.</summary>
    internal required long ApprovalSequence { get; init; }
}
