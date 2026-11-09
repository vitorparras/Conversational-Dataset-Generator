using Core.Ingestion.Entrance;
using Core.Persistence.Ingestion;
using Npgsql;

namespace Core.Host.Submissions;

/// <summary>
/// The service the screens and the routes of the ingestion call (decision 0052): it takes a file through the barrier, opens the batch that keeps what arrived,
/// rejects it with the reason when the barrier or the form turn it back, and answers the gate, the list of the batches and the detail of one.
/// Nothing is recorded for a request that is invalid, and everything is, the refused included, for one that is not.
/// </summary>
internal sealed class SubmissionService
{
    private const string NameWithPath = "name_with_path";
    private const int MostPerPage = 100;

    private readonly SubmissionIntake intake;
    private readonly IngestionBatches batches;
    private readonly BatchRegister register;
    private readonly NpgsqlDataSource dataSource;

    /// <summary>The service over the database and the pieces that take a file in.</summary>
    /// <param name="intake">The barrier, the conference and the conditions.</param>
    /// <param name="batches">Opens and closes the batches.</param>
    /// <param name="register">Reads the register of the submissions.</param>
    /// <param name="dataSource">The source of the connections, for the gate.</param>
    internal SubmissionService(SubmissionIntake intake, IngestionBatches batches, BatchRegister register, NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(intake);
        ArgumentNullException.ThrowIfNull(batches);
        ArgumentNullException.ThrowIfNull(register);
        ArgumentNullException.ThrowIfNull(dataSource);
        this.intake = intake;
        this.batches = batches;
        this.register = register;
        this.dataSource = dataSource;
    }

    /// <summary>Whether the ingestion takes material in: open, or blocked by the code of what is missing (decision 0014).</summary>
    /// <param name="cancellationToken">Cancels the question.</param>
    internal Task<SubmissionGateStatus> GateAsync(CancellationToken cancellationToken) => new IntakeDoor(dataSource).StatusAsync(cancellationToken);

    /// <summary>
    /// Takes a file in: validates the fields, passes the file through the barrier and the conference, and records the submission in a batch,
    /// rejected with the reason when it was turned back.
    /// </summary>
    /// <param name="request">Where it goes, in which form and format, under which condition, and the file.</param>
    /// <param name="cancellationToken">Cancels the submission.</param>
    internal Task<SubmissionOutcome> SubmitAsync(SubmissionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Content);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.AuthorId);
        return Problems(request) is { Count: > 0 } fields
            ? Task.FromResult(new SubmissionOutcome { Kind = SubmissionKind.Invalid, Fields = fields })
            : SubmittingAsync(request, cancellationToken);
    }

    /// <summary>The fields of the request that are missing or invalid, by name and by code; none when the request can be taken in.</summary>
    /// <param name="request">The request, whose file is not read.</param>
    internal IReadOnlyList<FieldProblem> Check(SubmissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Problems(request);
    }

    /// <summary>The batches of the domains granted, newest first, by state and by period, in a page the server cuts and never larger than the most a page may hold.</summary>
    /// <param name="filter">The domains granted, the state, the period and the page.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    internal Task<IReadOnlyList<BatchEntry>> ListAsync(BatchFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return register.ListAsync(filter with { Take = filter.Take > MostPerPage ? MostPerPage : filter.Take }, cancellationToken);
    }

    /// <summary>One batch with what its submission brought, if it is in one of the domains granted.</summary>
    /// <param name="granted">The domains granted.</param>
    /// <param name="batchId">The batch.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    internal Task<BatchEntry?> DetailAsync(IEnumerable<int> granted, long batchId, CancellationToken cancellationToken) =>
        register.ReadAsync(granted, batchId, cancellationToken);

    private static bool HasPath(string? name) =>
        name is not null && (HasSeparator(name) || name.Contains("..", StringComparison.Ordinal) || name.Any(char.IsControl));

    private static bool HasSeparator(string name) => name.Contains('/', StringComparison.Ordinal) || name.Contains('\\', StringComparison.Ordinal);

    private static SubmissionOutcome Answer(SubmissionResult received, long batch, (SubmissionReason? Reason, string? Warning) judged, string origin) => new()
    {
        Kind = SubmissionKind.Done,
        Submission = received.Id.ToString(),
        Batch = batch,
        State = judged.Reason is null ? SubmissionState.Accepted : SubmissionState.Rejected,
        Reason = judged.Reason,
        Size = received.BytesRead,
        Sha256 = received.Digest,
        Warning = judged.Warning,
        Origin = origin,
    };

    private static FieldProblem? When(bool wrong, string field, string code) => wrong ? new FieldProblem(field, code) : null;

    private List<FieldProblem> Problems(SubmissionRequest request)
    {
        var problems = new[]
        {
            When(request.DomainId <= 0, "domain", "missing"),
            When(!Enum.IsDefined(request.DeclaredForm), "declared_form", "invalid"),
            When(string.IsNullOrWhiteSpace(request.SourceFormat), "source_format", "missing"),
            When(request.Condition is { } declared && intake.Conditions.RankOf(declared) < 0, "condition", "unknown"),
            SubmissionOrigin.Problem(request) is { } origin ? new FieldProblem("origin", origin) : null,
        }.OfType<FieldProblem>().ToList();
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
        var reason = SubmissionJudgement.Of(intake, received, request.DeclaredForm);
        var warning = HasPath(request.ReceivedName) ? NameWithPath : null;
        var receipt = new SubmissionReceipt(received.Id.ToString(), received.BytesRead, received.Digest, warning);
        if (SubmissionOrigin.Declaration(intake, request) is not { } origin)
        {
            return new SubmissionOutcome { Kind = SubmissionKind.NotRecorded, Failure = BatchOutcome.DomainUnknown };
        }

        var opening = new BatchOpening(request.DomainId, request.AuthorId, request.Condition, new SubmissionOpening(receipt, origin)) { RejectedFor = reason?.Code };
        var opened = await batches.OpenAsync(opening, cancellationToken).ConfigureAwait(false);
        var done = reason is null ? BatchOutcome.Opened : BatchOutcome.Rejected;
        return opened.Outcome == done
            ? Answer(received, opened.BatchId!.Value, (reason, warning), origin.State)
            : new SubmissionOutcome { Kind = SubmissionKind.NotRecorded, Failure = opened.Outcome };
    }
}
