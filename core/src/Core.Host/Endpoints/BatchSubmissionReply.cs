using Core.Host.Screens;
using Core.Host.Submissions;
using Core.Persistence.Ingestion;
using SubmissionFields = Core.Host.Screens.SubmissionFields;

namespace Core.Host.Endpoints;

/// <summary>
/// What the page of the submission (#607) answers once the form was read: the gate, the fields at fault before the file is read, and then the outcome of the service, each with its status.
/// An accepted file leads to its open batch; nothing of the material is said back.
/// </summary>
internal static class BatchSubmissionReply
{
    /// <summary>The gate, the fields and the service, in this order, so a closed ingestion or a field at fault never reaches the reading of the file.</summary>
    internal static async Task<IResult> SendAsync(HttpContext context, (SubmissionService Service, SubmissionFields.Choices Offer) sending, SubmissionRead read)
    {
        var gate = await sending.Service.GateAsync(context.RequestAborted);
        if (!gate.Open)
        {
            return SubmissionPageView.Page(context, sending.Offer, SubmissionPageView.Answered(read, nameof(SubmissionScreen.BlockedBy), gate.Code), StatusCodes.Status503ServiceUnavailable);
        }

        var problems = Problems(sending.Service, read);
        if (problems.Count > 0)
        {
            return SubmissionPageView.Page(context, sending.Offer, SubmissionPageView.Answered(read, nameof(SubmissionScreen.Errors), problems), StatusCodes.Status400BadRequest);
        }

        var outcome = await sending.Service.SubmitAsync(read.Request, context.RequestAborted);
        return outcome.Kind == SubmissionKind.Done && outcome.State == SubmissionState.Accepted
            ? PageRedirect.Of(context, ScreenPaths.BatchOf(outcome.Batch!.Value))
            : Refused(context, sending.Offer, read, outcome);
    }

    /// <summary>What the service answered when the file did not open a batch for review: the verdict, the fields at fault, the gate, or the trouble of the host, each with its status.</summary>
    private static IResult Refused(HttpContext context, SubmissionFields.Choices offer, SubmissionRead read, SubmissionOutcome outcome) => outcome.Kind switch
    {
        SubmissionKind.Done => Shown(context, offer, (read, nameof(SubmissionScreen.Answer), VerdictOf(outcome)), StatusCodes.Status200OK),
        SubmissionKind.Invalid => Shown(context, offer, (read, nameof(SubmissionScreen.Errors), ErrorsOf(outcome.Fields!)), StatusCodes.Status400BadRequest),
        SubmissionKind.Blocked => Shown(context, offer, (read, nameof(SubmissionScreen.BlockedBy), outcome.BlockedBy), StatusCodes.Status503ServiceUnavailable),
        SubmissionKind.Unavailable => Troubled(context, offer, read, SubmissionScreen.Troubles.NotKept),
        SubmissionKind.NotRecorded when outcome.Failure == BatchOutcome.DomainUnknown => ScreenResult.NotFound(context),
        SubmissionKind.NotRecorded => Troubled(context, offer, read, SubmissionScreen.Troubles.NotRecorded),
        SubmissionKind.Collided => Troubled(context, offer, read, SubmissionScreen.Troubles.Collided),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome.Kind, "The kind of the outcome is not one the service answers."),
    };

    private static IResult Troubled(HttpContext context, SubmissionFields.Choices offer, SubmissionRead read, SubmissionScreen.Troubles trouble) =>
        Shown(context, offer, (read, nameof(SubmissionScreen.Trouble), trouble), StatusCodes.Status503ServiceUnavailable);

    private static IResult Shown(HttpContext context, SubmissionFields.Choices offer, (SubmissionRead Read, string Key, object? Value) answer, int status) =>
        SubmissionPageView.Page(context, offer, SubmissionPageView.Answered(answer.Read, answer.Key, answer.Value), status);

    /// <summary>The fields at fault before the file is read: the ones the service finds when the condition must be chosen, and a sending with no file.</summary>
    private static Dictionary<string, string> Problems(SubmissionService service, SubmissionRead read)
    {
        var problems = ErrorsOf(service.CheckChosen(read.Request));
        if (!read.HasFile)
        {
            _ = problems.TryAdd(SubmissionTexts.FileField, "missing");
        }

        return problems;
    }

    private static Dictionary<string, string> ErrorsOf(IEnumerable<FieldProblem> problems)
    {
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var problem in problems)
        {
            _ = errors.TryAdd(problem.Field, problem.Code);
        }

        return errors;
    }

    private static SubmissionVerdict.Result VerdictOf(SubmissionOutcome outcome)
    {
        var batch = outcome.Batch!.Value;
        var points = new List<SubmissionVerdict.Point>();
        if (outcome.Reason is { } reason)
        {
            points.AddRange(reason.Findings.Select(finding => new SubmissionVerdict.Point(finding.Unit, finding.Field)));
        }

        return new SubmissionVerdict.Result(batch, ScreenPaths.BatchOf(batch), outcome.Size, outcome.Sha256!, outcome.Origin == "declared", outcome.Reason?.Code, points, outcome.Warning);
    }
}
