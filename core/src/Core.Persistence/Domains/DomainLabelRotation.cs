using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Platform.Keys;
using Npgsql;

namespace Core.Persistence.Domains;

/// <summary>
/// The rotation of the label of a domain (decision 0045, issue 56): the owner credential writes the labels of the next generation and the event of the chain,
/// in one serializable transaction, and either both exist or neither does. Before a rotation writes anything the custody has to hold the labels of the next generation (operacao/002), since content
/// encrypted under a label the custody lacks could not be opened again. No label is changed or removed: the labels of the older generations stay and decrypt what was encrypted under them.
/// </summary>
public sealed class DomainLabelRotation
{
    private readonly NpgsqlDataSource dataSource;

    /// <summary>The rotation writes through the data source built with the credential of the owner, which is the one allowed to add a label.</summary>
    public DomainLabelRotation(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <summary>The labels of derivation a domain takes in the generation given, one for each purpose, in the order of the purposes.</summary>
    public static IReadOnlyList<string> LabelsOf(int domainId, int version)
    {
        var labels = new List<string>();
        foreach (var purpose in Enum.GetValues<LabelPurpose>().Order())
        {
            labels.Add(new DerivationLabel(purpose, domainId, version).Text);
        }

        return labels;
    }

    /// <summary>Passes the domain to the next generation of its labels if the custody holds them; a refusal writes nothing, and a missing label is named.</summary>
    /// <param name="domainId">The domain to rotate.</param>
    /// <param name="custody">The derivation labels the custody file holds.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public Task<DomainLabelResult> RotateAsync(int domainId, DerivationLabels custody, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(custody);
        return RunAsync((scope, answer) => RotateInScopeAsync(scope, (domainId, custody), answer, cancellationToken), cancellationToken);
    }

    private async Task<DomainLabelResult> RunAsync(Func<CustodyScope, Answer, Task<CustodyResult?>> body, CancellationToken cancellationToken)
    {
        var answer = new Answer();
        _ = await CustodyTransaction.RunAsync(dataSource, scope => body(scope, answer), cancellationToken).ConfigureAwait(false);
        return answer.Result ?? new DomainLabelResult { Outcome = DomainLabelOutcome.Conflict };
    }

    private static async Task<CustodyResult?> RotateInScopeAsync(CustodyScope scope, (int Domain, DerivationLabels Custody) request, Answer answer, CancellationToken cancellationToken)
    {
        var newest = await DomainLabelQueries.NewestVersionAsync(scope, request.Domain, cancellationToken).ConfigureAwait(false);
        if (newest == 0)
        {
            answer.Result = new DomainLabelResult { Outcome = DomainLabelOutcome.DomainNotFound, DomainId = request.Domain };
            return null;
        }

        var next = newest + 1;
        var labels = LabelsOf(request.Domain, next);
        var lacking = request.Custody.Lacking(labels);
        if (lacking.Count > 0)
        {
            answer.Result = new DomainLabelResult { Outcome = DomainLabelOutcome.LabelsMissing, DomainId = request.Domain, Version = next, MissingLabels = lacking };
            return null;
        }

        var change = new Change(request.Domain, next, labels, CustodyEvent.DomainLabelRotated(request.Domain, next));
        return await WriteAsync(scope, change, answer, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes the labels and then the event, and commits only if the chain took the event.</summary>
    private static async Task<CustodyResult?> WriteAsync(CustodyScope scope, Change change, Answer answer, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var label in change.Labels)
            {
                await DomainLabelQueries.AddLabelAsync(scope, label, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (PostgresException taken) when (taken.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return null;
        }

        var chain = await CustodyTransaction.AppendAsync(scope, change.Pending, cancellationToken).ConfigureAwait(false);
        if (chain.Outcome != CustodyOutcome.Appended)
        {
            var outcome = chain.Outcome == CustodyOutcome.Conflict ? DomainLabelOutcome.Conflict : DomainLabelOutcome.ChainRefused;
            answer.Result = new DomainLabelResult { Outcome = outcome, DomainId = change.Domain };
            return null;
        }

        await scope.CommitAsync(cancellationToken).ConfigureAwait(false);
        answer.Result = new DomainLabelResult { Outcome = DomainLabelOutcome.Rotated, DomainId = change.Domain, Version = change.Version, Sequence = chain.Sequence };
        return chain;
    }

    private sealed record Change(int Domain, int Version, IReadOnlyList<string> Labels, CustodyEvent Pending);

    private sealed class Answer
    {
        internal DomainLabelResult? Result { get; set; }
    }
}
