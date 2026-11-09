using Core.Host.Submissions;
using Core.Persistence.Ingestion;

namespace Core.Host.Endpoints;

/// <summary>
/// How the outcome of the service is answered: <c>201</c> with what the submission brought, <c>413</c> with the same body and the state rejected, <c>400</c> with the fields at fault,
/// <c>409</c> with the code of what keeps the gate closed, and nothing in any of them that is a word of the material, the name the file came with or the text of the origin.
/// </summary>
internal static class SubmissionAnswer
{
    internal static IResult Of(SubmissionOutcome outcome) => outcome.Kind switch
    {
        SubmissionKind.Invalid => Results.Json(new FieldsRefused([.. outcome.Fields!.Select(Field)]), statusCode: StatusCodes.Status400BadRequest),
        SubmissionKind.Blocked => Results.Json(new SubmissionBlocked(outcome.BlockedBy), statusCode: StatusCodes.Status409Conflict),
        SubmissionKind.NotRecorded => NotRecorded(outcome.Failure),
        SubmissionKind.Done => Results.Json(Done(outcome), statusCode: outcome.IsTooLarge ? StatusCodes.Status413PayloadTooLarge : StatusCodes.Status201Created),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome.Kind, "The kind of the outcome is not one the service answers."),
    };

    /// <summary>The fields at fault when the body had no file, beside the ones the service found.</summary>
    internal static IResult NoFile(IEnumerable<FieldProblem> problems) =>
        Results.Json(new FieldsRefused([.. problems.Select(Field), new FieldShown("file", "missing")]), statusCode: StatusCodes.Status400BadRequest);

    /// <summary>A batch as the list shows it.</summary>
    internal static BatchShown Shown(BatchEntry entry) => new(
        entry.Id,
        entry.DomainId,
        ReasonCodes.Of(entry.State),
        entry.Reason,
        entry.SubmittedAt.ToUniversalTime(),
        entry.SubmittedBy,
        entry.Receipt?.Submission,
        entry.Receipt?.Bytes,
        entry.Receipt?.Digest,
        WarningsOf(entry.Receipt?.Warning),
        entry.OriginState);

    /// <summary>The field as the form names it, which is the name the caller sent, not the one the service keeps.</summary>
    private static FieldShown Field(FieldProblem problem) => new(problem.Field switch { "declared_form" => "declaredForm", "source_format" => "sourceFormat", _ => problem.Field }, problem.Code);

    private static IReadOnlyList<string> WarningsOf(string? warning) => warning is null ? Array.Empty<string>() : Enumerable.Repeat(warning, 1).ToArray();

    /// <summary>A domain the host does not know is a domain that does not exist; any other failure to record is the host's, not the request's.</summary>
    private static IResult NotRecorded(BatchOutcome? failure) =>
        failure == BatchOutcome.DomainUnknown ? Results.NotFound() : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

    private static SubmissionAnswered Done(SubmissionOutcome outcome) => new(
        outcome.Submission!,
        outcome.Batch!.Value,
        outcome.State == SubmissionState.Accepted ? "accepted" : "rejected",
        outcome.Reason is { } reason ? new ReasonShown(reason.Code, [.. reason.Findings.Select(finding => new FindingShown(finding.Unit, finding.Field))]) : null,
        outcome.Size,
        outcome.Sha256!,
        WarningsOf(outcome.Warning),
        outcome.Origin!);
}
