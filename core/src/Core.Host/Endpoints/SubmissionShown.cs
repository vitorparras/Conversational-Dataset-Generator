namespace Core.Host.Endpoints;

/// <summary>The answer of <c>400</c>: every field at fault, and nothing was recorded.</summary>
/// <param name="Fields">The fields.</param>
internal sealed record FieldsRefused(IReadOnlyList<FieldShown> Fields);

/// <summary>Where the material did not hold: the position of the unit and the field of it, never what it says.</summary>
/// <param name="Unit">The position, from zero; null when it is about the material as a whole.</param>
/// <param name="Field">The field, as a code; null when it is about the unit or the material as a whole.</param>
internal sealed record FindingShown(int? Unit, string? Field);

/// <summary>Why the submission was turned back: the stable code and where the material did not hold.</summary>
/// <param name="Code">The code.</param>
/// <param name="Findings">Where it did not hold.</param>
internal sealed record ReasonShown(string Code, IReadOnlyList<FindingShown> Findings);

/// <summary>
/// The answer of <c>201</c>, and of <c>413</c> with the state rejected: what the submission brought and how it ended. Nothing in it is a word of the material, the name the file came with
/// or the text of the origin, which is answered only as <c>declared</c> or <c>unknown</c>.
/// </summary>
/// <param name="Submission">The identifier the system generated.</param>
/// <param name="Batch">The batch the submission opened.</param>
/// <param name="State">The state: <c>accepted</c> or <c>rejected</c>.</param>
/// <param name="Reason">Why it was rejected; null when it was accepted.</param>
/// <param name="Size">How many bytes were read.</param>
/// <param name="Sha256">The SHA-256 of the bytes read, in lowercase hexadecimal.</param>
/// <param name="Warnings">The codes of what the service noticed and let pass.</param>
/// <param name="PreviousBatch">For a repeat, the batch that accepted the same file before (<c>previous_batch</c>); null for any other submission.</param>
/// <param name="Origin"><c>declared</c> or <c>unknown</c>.</param>
internal sealed record SubmissionAnswered(
    string Submission, long Batch, string State, ReasonShown? Reason, long Size, string Sha256, IReadOnlyList<string> Warnings, string Origin, long? PreviousBatch);

/// <summary>The answer when the gate of the ingestion is closed: nothing was read and nothing was recorded.</summary>
/// <param name="BlockedBy">The code of what keeps the gate closed.</param>
internal sealed record SubmissionBlocked(string? BlockedBy);

/// <summary>The answer of the gate: <c>open</c>, or <c>blocked</c> with the code of what is missing.</summary>
/// <param name="State"><c>open</c> or <c>blocked</c>.</param>
/// <param name="Code">The code of what keeps it closed; null when it is open.</param>
internal sealed record GateShown(string State, string? Code);

/// <summary>A batch as the list shows it: nothing in it is a word of the material or the name of the file. The instants are UTC.</summary>
/// <param name="Id">The batch.</param>
/// <param name="Domain">The domain it belongs to.</param>
/// <param name="State"><c>open</c>, <c>accepted</c> or <c>rejected</c>.</param>
/// <param name="Reason">The code of the rejection; null while the batch is not rejected.</param>
/// <param name="SubmittedAt">When the submission came.</param>
/// <param name="SubmittedBy">The person who submitted.</param>
/// <param name="Submission">The identifier generated; null for a batch no submission opened.</param>
/// <param name="Size">How many bytes were read; null for a batch no submission opened.</param>
/// <param name="Sha256">The SHA-256 of what came; null for a batch no submission opened.</param>
/// <param name="Warnings">The codes of what the service noticed and let pass.</param>
/// <param name="Origin"><c>declared</c> or <c>unknown</c>; null for a batch no submission opened.</param>
/// <param name="PreviousBatch">For a repeat, the batch that accepted the same file before (<c>previous_batch</c>); null for any other batch.</param>
internal sealed record BatchShown(
    long Id, int Domain, string State, string? Reason, DateTimeOffset SubmittedAt, long SubmittedBy, string? Submission, long? Size, string? Sha256,
    IReadOnlyList<string> Warnings, string? Origin, long? PreviousBatch);

/// <summary>A page of the list: the batches of the domains granted, newest first.</summary>
/// <param name="Page">The page, from one.</param>
/// <param name="PageSize">How many batches a page holds.</param>
/// <param name="Batches">The batches of the page.</param>
internal sealed record BatchesShown(int Page, int PageSize, IReadOnlyList<BatchShown> Batches);

/// <summary>The answer when the path could not keep the material: nothing of it was kept, the batch is registered as rejected, and the code says why, never a word of the material.</summary>
/// <param name="Code">
/// The code: <c>content_key_unavailable</c> or <c>write_failed</c>;
/// <c>concurrent_submission</c> when another submission reached the same link first, again and again, and the same file may be sent again.
/// </param>
/// <param name="Reason">What was wrong with the key when it is the key: <c>absent</c>, <c>unreadable</c> or <c>wrong_size</c>; null otherwise.</param>
/// <param name="Batch">The batch registered as rejected, with no item; null when none could be registered. Null also when the collision was in the opening.</param>
internal sealed record SubmissionUnavailable(string Code, string? Reason, long? Batch);
