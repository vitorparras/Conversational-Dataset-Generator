using Core.Persistence.Ingestion;

namespace Core.Host.Submissions;

/// <summary>How a submission ended for the service: done (accepted or rejected, both recorded), refused before anything was recorded, or not recorded for a reason of the batch.</summary>
internal enum SubmissionKind
{
    /// <summary>The submission was recorded in a batch, which is open or rejected.</summary>
    Done,

    /// <summary>A field is missing or invalid; nothing was read and nothing was recorded.</summary>
    Invalid,

    /// <summary>The gate of the ingestion is closed; nothing was read and nothing was recorded.</summary>
    Blocked,

    /// <summary>The batch could not be opened or could not be closed; <see cref="SubmissionOutcome.Failure" /> says why.</summary>
    NotRecorded,

    /// <summary>The path could not keep the material: nothing is kept, and the code says why.</summary>
    Unavailable,
}

/// <summary>Whether the barrier and the form accepted the material, or turned it back; both are recorded.</summary>
internal enum SubmissionState
{
    /// <summary>The barrier and the form accepted it; its batch stays open for what comes next.</summary>
    Accepted,

    /// <summary>It was turned back, and its batch is rejected with the reason as its motive.</summary>
    Rejected,
}

/// <summary>A field of the request that is missing or invalid, by name and by code, never by value.</summary>
/// <param name="Field">The field.</param>
/// <param name="Code">Why: <c>missing</c>, <c>invalid</c>, <c>conflict</c>, <c>too_long</c> or <c>unknown</c>.</param>
internal sealed record FieldProblem(string Field, string Code);

/// <summary>Where a refusal points: the position of the unit and the field of it, never what it says.</summary>
/// <param name="Unit">The position, from zero; null when it is about the material as a whole.</param>
/// <param name="Field">The field, as a code; null when it is about the unit or the material as a whole.</param>
internal sealed record SubmissionFinding(int? Unit, string? Field);

/// <summary>Why the submission was turned back: a stable code, the cause the reader gave when it could not read, and where the material did not hold.</summary>
/// <param name="Code">The code, a name of lowercase letters, digits and underscores.</param>
/// <param name="Findings">Where it did not hold; none when the refusal is about the file as a whole.</param>
internal sealed record SubmissionReason(string Code, IReadOnlyList<SubmissionFinding> Findings);

/// <summary>What the service answers a submission with. Nothing in it is a word of the material or the name the file came with.</summary>
internal sealed record SubmissionOutcome
{
    /// <summary>How it ended.</summary>
    public required SubmissionKind Kind { get; init; }

    /// <summary>The fields that were missing or invalid, for <see cref="SubmissionKind.Invalid" />; null otherwise.</summary>
    public IReadOnlyList<FieldProblem>? Fields { get; init; }

    /// <summary>The code of what keeps the gate closed, for <see cref="SubmissionKind.Blocked" />.</summary>
    public string? BlockedBy { get; init; }

    /// <summary>The code of what made the path unavailable, for <see cref="SubmissionKind.Unavailable"/>.</summary>
    public string? Code { get; init; }

    /// <summary>Why the batch was not recorded, for <see cref="SubmissionKind.NotRecorded" />.</summary>
    public BatchOutcome? Failure { get; init; }

    /// <summary>The identifier the system generated for the submission, as 32 hexadecimal digits.</summary>
    public string? Submission { get; init; }

    /// <summary>The batch the submission opened.</summary>
    public long? Batch { get; init; }

    /// <summary>Accepted or rejected.</summary>
    public SubmissionState? State { get; init; }

    /// <summary>Why it was rejected; null when it was accepted.</summary>
    public SubmissionReason? Reason { get; init; }

    /// <summary>How many bytes were read.</summary>
    public long Size { get; init; }

    /// <summary>The SHA-256 of the bytes read.</summary>
    public string? Sha256 { get; init; }

    /// <summary>Whether the origin was <c>declared</c> or <c>unknown</c>; the text of it is never answered.</summary>
    public string? Origin { get; init; }

    /// <summary>The code of what the service noticed and let pass, as a name that had a path in it; null when it noticed nothing.</summary>
    public string? Warning { get; init; }

    /// <summary>Whether the file passed the limit of bytes, which the route answers with 413.</summary>
    public bool IsTooLarge => Reason?.Code == ReasonCodes.TooLarge;
}
