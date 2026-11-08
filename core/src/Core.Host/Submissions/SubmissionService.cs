using Core.Ingestion;
using Core.Ingestion.Entrance;
using Core.Persistence.Custody;
using Core.Persistence.Ingestion;
using Core.Persistence.Recovery;
using Npgsql;

namespace Core.Host.Submissions;

/// <summary>
/// The service the screens and the routes of the ingestion call (decision 0052): it takes a file through the barrier, opens the batch that keeps what arrived,
/// rejects it with the reason when the barrier or the form turn it back, and answers the gate.
/// Nothing is recorded for a request that is invalid, and everything is, the refused included, for one that is not.
/// </summary>
internal sealed class SubmissionService
{
    private const string NameWithPath = "name_with_path";

    private readonly SubmissionIntake intake;
    private readonly IngestionBatches batches;
    private readonly NpgsqlDataSource dataSource;

    /// <summary>The service over the database and the pieces that take a file in.</summary>
    /// <param name="intake">The barrier, the conference and the conditions.</param>
    /// <param name="batches">Opens and closes the batches.</param>
    /// <param name="dataSource">The source of the connections, for the gate.</param>
    public SubmissionService(SubmissionIntake intake, IngestionBatches batches, NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(intake);
        ArgumentNullException.ThrowIfNull(batches);
        ArgumentNullException.ThrowIfNull(dataSource);
        this.intake = intake;
        this.batches = batches;
        this.dataSource = dataSource;
    }

    /// <summary>Whether the ingestion takes material in: open, or blocked by the code of what is missing (decision 0014).</summary>
    /// <param name="cancellationToken">Cancels the question.</param>
    public async Task<SubmissionGateStatus> GateAsync(CancellationToken cancellationToken)
    {
        var status = await RecoveryGate.StatusAsync(dataSource, cancellationToken).ConfigureAwait(false);
        return status.Tested
            ? new SubmissionGateStatus(true, null)
            : new SubmissionGateStatus(false, status.Missing == RecoveryGate.StaleWords ? "recovery_rehearsal_stale" : "recovery_not_rehearsed");
    }

    /// <summary>
    /// Takes a file in: validates the fields, passes the file through the barrier and the conference, and records the submission in a batch,
    /// rejected with the reason when it was turned back.
    /// </summary>
    /// <param name="request">Where it goes, in which form and format, under which condition, and the file.</param>
    /// <param name="cancellationToken">Cancels the submission.</param>
    public Task<SubmissionOutcome> SubmitAsync(SubmissionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Content);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.AuthorId);
        return Problems(request) is { Count: > 0 } fields
            ? Task.FromResult(new SubmissionOutcome { Kind = SubmissionKind.Invalid, Fields = fields })
            : SubmittingAsync(request, cancellationToken);
    }

    private static bool HasPath(string? name) =>
        name is not null
        && (name.Contains('/', StringComparison.Ordinal) || name.Contains('\\', StringComparison.Ordinal) || name.Contains("..", StringComparison.Ordinal) || name.Any(char.IsControl));

    private SubmissionReason? Judged(SubmissionResult received, MaterialForm form) =>
        Barred(received) ?? (intake.Conference.Confer(form, ReadOutcome.Read(received.Material!)).Refusal is { } refused ? ReasonOf(refused) : null);

    private static SubmissionReason? Barred(SubmissionResult received) =>
        received.Refusal is { } refusal ? new SubmissionReason(ReasonCodes.Of(refusal), new List<SubmissionFinding>()) : null;

    private static SubmissionReason ReasonOf(Refusal refusal)
    {
        var findings = refusal.Findings.Select(finding => new SubmissionFinding(finding.Unit, finding.Field is { } field ? ReasonCodes.Of(field) : null)).ToList();
        return new SubmissionReason(ReasonCodes.Of(refusal.Code), findings);
    }

    private static SubmissionOutcome Answer(SubmissionResult received, long batch, SubmissionReason? reason, string? warning) => new()
    {
        Kind = SubmissionKind.Done,
        Submission = received.Id.ToString(),
        Batch = batch,
        State = reason is null ? SubmissionState.Accepted : SubmissionState.Rejected,
        Reason = reason,
        Size = received.BytesRead,
        Sha256 = received.Digest,
        Warning = warning,
    };

    private static void AddWhen(List<FieldProblem> problems, bool wrong, string field, string code)
    {
        if (wrong)
        {
            problems.Add(new FieldProblem(field, code));
        }
    }

    private List<FieldProblem> Problems(SubmissionRequest request)
    {
        var problems = new List<FieldProblem>();
        AddWhen(problems, request.DomainId <= 0, "domain", "missing");
        AddWhen(problems, !Enum.IsDefined(request.DeclaredForm), "declared_form", "invalid");
        AddWhen(problems, string.IsNullOrWhiteSpace(request.SourceFormat), "source_format", "missing");
        AddWhen(problems, request.Condition is { } declared && intake.Conditions.RankOf(declared) < 0, "condition", "unknown");
        return problems;
    }

    private async Task<SubmissionOutcome> SubmittingAsync(SubmissionRequest request, CancellationToken cancellationToken)
    {
        var gate = await GateAsync(cancellationToken).ConfigureAwait(false);
        if (!gate.Open)
        {
            return new SubmissionOutcome { Kind = SubmissionKind.Blocked, BlockedBy = gate.Code };
        }

        var received = await intake.Gate.ReceiveAsync(new Submission(request.Content, request.SourceFormat, request.ReceivedName), cancellationToken).ConfigureAwait(false);
        var reason = Judged(received, request.DeclaredForm);
        var warning = HasPath(request.ReceivedName) ? NameWithPath : null;
        var receipt = new SubmissionReceipt(received.Id.ToString(), received.BytesRead, received.Digest, warning);
        var opened = await batches.OpenAsync(new BatchOpening(request.DomainId, request.AuthorId, request.Condition, receipt), cancellationToken).ConfigureAwait(false);
        if (opened.Outcome != BatchOutcome.Opened)
        {
            return new SubmissionOutcome { Kind = SubmissionKind.NotRecorded, Failure = opened.Outcome };
        }

        var batch = opened.BatchId!.Value;
        if (reason is null)
        {
            return Answer(received, batch, null, warning);
        }

        var closed = await batches.RejectAsync(new BatchClosing(request.DomainId, batch, CustodyAuthor.System), reason.Code, cancellationToken).ConfigureAwait(false);
        return closed.Outcome == BatchOutcome.Rejected
            ? Answer(received, batch, reason, warning)
            : new SubmissionOutcome { Kind = SubmissionKind.NotRecorded, Failure = closed.Outcome, Batch = batch };
    }
}
