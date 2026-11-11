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
    private const int MostPerPage = 100;

    private readonly SubmissionIntake intake;
    private readonly IngestionBatches batches;
    private readonly BatchRegister register;
    private readonly NpgsqlDataSource dataSource;
    private readonly SubmissionStoring.Keeper keeper;

    /// <summary>The service over the database and the pieces that take a file in.</summary>
    /// <param name="intake">The barrier, the conference and the conditions.</param>
    /// <param name="batches">Opens and closes the batches.</param>
    /// <param name="dataSource">The source of the connections, for the gate and for the register of the submissions.</param>
    /// <param name="logger">Says, by the batch and the domain only, that a batch could not be tidied.</param>
    internal SubmissionService(SubmissionIntake intake, IngestionBatches batches, NpgsqlDataSource dataSource, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(intake);
        ArgumentNullException.ThrowIfNull(batches);
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(logger);
        this.intake = intake;
        this.batches = batches;
        register = new BatchRegister(dataSource);
        this.dataSource = dataSource;
        keeper = new SubmissionStoring.Keeper(batches, intake, logger);
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
        return SubmissionFields.Of(intake, request) is { Count: > 0 } fields
            ? Task.FromResult(new SubmissionOutcome { Kind = SubmissionKind.Invalid, Fields = fields })
            : SubmittingAsync(request, cancellationToken);
    }

    /// <summary>The fields of the request that are missing or invalid, by name and by code; none when the request can be taken in.</summary>
    /// <param name="request">The request, whose file is not read.</param>
    internal IReadOnlyList<FieldProblem> Check(SubmissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SubmissionFields.Of(intake, request);
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

    private async Task<SubmissionOutcome> SubmittingAsync(SubmissionRequest request, CancellationToken cancellationToken)
    {
        var gate = await GateAsync(cancellationToken).ConfigureAwait(false);
        if (!gate.Open)
        {
            return new SubmissionOutcome { Kind = SubmissionKind.Blocked, BlockedBy = gate.Code };
        }

        var received = await intake.Gate.ReceiveAsync(new Submission(request.Content, request.SourceFormat, request.ReceivedName), cancellationToken).ConfigureAwait(false);
        return await OpeningAsync(request, received, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Judges what was received and opens the batch that keeps it, rejected with the reason when it was turned back.</summary>
    private async Task<SubmissionOutcome> OpeningAsync(SubmissionRequest request, SubmissionResult received, CancellationToken cancellationToken)
    {
        var reason = await SubmissionJudgement.OfAsync(intake, register, request, received, cancellationToken).ConfigureAwait(false);
        var warning = SubmissionOrigin.WarningOf(request.ReceivedName);
        var receipt = new SubmissionReceipt(received.Id.ToString(), received.BytesRead, received.Digest, warning);
        if (SubmissionOrigin.Opening(intake, request, receipt) is not { } opening || (reason is null && !SubmissionStoring.CanSeal(intake.Sealing, request.DomainId)))
        {
            return new SubmissionOutcome { Kind = SubmissionKind.NotRecorded, Failure = BatchOutcome.DomainUnknown };
        }

        var opened = await batches.OpenAsync(opening with { RejectedFor = reason?.Code }, cancellationToken).ConfigureAwait(false);
        if (Unrecorded(opened, reason is not null) is { } unrecorded)
        {
            return unrecorded;
        }

        var kept = new Opened(received, opened.BatchId!.Value, warning, opening.Origin!.State);
        return reason is null ? await StoringAsync(request, kept, cancellationToken).ConfigureAwait(false) : Turned(kept, reason);
    }

    /// <summary>What there is to say when the opening did not do what was asked: a collision that the batches gave up on, or the outcome that kept it; none when it did.</summary>
    private static SubmissionOutcome? Unrecorded(BatchResult opened, bool turnedBack)
    {
        if (opened.Collided)
        {
            return new SubmissionOutcome { Kind = SubmissionKind.Collided };
        }

        var wanted = turnedBack ? BatchOutcome.Rejected : BatchOutcome.Opened;
        return opened.Outcome == wanted ? null : new SubmissionOutcome { Kind = SubmissionKind.NotRecorded, Failure = opened.Outcome };
    }

    /// <summary>A file that was not turned back has its items written and is accepted, or is rejected with no item when they cannot be.</summary>
    private async Task<SubmissionOutcome> StoringAsync(SubmissionRequest request, Opened opened, CancellationToken cancellationToken)
    {
        var place = new SubmissionStoring.Kept(request.DomainId, opened.Batch, opened.Received.Material!, request.DeclaredForm);
        var kept = await keeper.KeepAsync(place, cancellationToken).ConfigureAwait(false);
        return SubmissionStoring.IsRepeat(kept)
            ? await RepeatedAsync(request, opened, cancellationToken).ConfigureAwait(false)
            : SubmissionStoring.Failure(kept, opened.Batch) ?? Answer(opened.Received, opened.Batch, (null, opened.Warning), opened.Origin);
    }

    /// <summary>A file sent at the same time as an identical one, which the database let the other one accept: it is a repeat of that one, rejected with no item as any other.</summary>
    private async Task<SubmissionOutcome> RepeatedAsync(SubmissionRequest request, Opened opened, CancellationToken cancellationToken)
    {
        var previous = await register.PreviousAsync(request.DomainId, opened.Received.Digest, cancellationToken).ConfigureAwait(false);
        return Turned(opened, SubmissionJudgement.RepeatOf(previous));
    }

    /// <summary>What there is to say of a submission that was registered as rejected: the key that was missing is a failure of the service, not a defect of the file.</summary>
    private static SubmissionOutcome Turned(Opened opened, SubmissionReason reason) => reason.Code == SubmissionStoring.KeyUnavailable
        ? new SubmissionOutcome { Kind = SubmissionKind.Unavailable, Code = reason.Code, Batch = opened.Batch }
        : Answer(opened.Received, opened.Batch, (reason, opened.Warning), opened.Origin);

    private sealed record Opened(SubmissionResult Received, long Batch, string? Warning, string Origin);
}
