namespace Core.Host.Settings;

/// <summary>
/// A change of configuration approved on the screen, as the applier reads it from the database (decision 0053): what was submitted, who submitted and who approved, and the sequences of the
/// two events of the chain that the applier checks before it touches the file.
/// </summary>
internal sealed record ApprovedChange
{
    /// <summary>The submission.</summary>
    public required long Id { get; init; }

    /// <summary>The key, as <c>Section:Name</c>.</summary>
    public required string Key { get; init; }

    /// <summary>The new value.</summary>
    public required string Value { get; init; }

    /// <summary>The person who submitted.</summary>
    public required long SubmitterId { get; init; }

    /// <summary>The sequence of the event of the submission.</summary>
    public required long SubmissionSequence { get; init; }

    /// <summary>The person who approved.</summary>
    public required long ApproverId { get; init; }

    /// <summary>The sequence of the event of the approval.</summary>
    public required long ApprovalSequence { get; init; }
}
