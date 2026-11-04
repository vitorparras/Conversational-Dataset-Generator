using System.Runtime.CompilerServices;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Chain.Append;
using Core.Platform.Keys;
using Npgsql;

namespace Core.Persistence.Domains;

/// <summary>
/// The rotation of the label of a domain (decision 0045, issue 56): the owner credential writes the labels of the next generation and the event of the chain in one serializable transaction,
/// and only if the custody holds those labels (operacao/002), since content encrypted under a label it lacks could not be opened again. No label is changed or removed: the older ones stay.
/// </summary>
public sealed class DomainLabelRotation
{
    private readonly NpgsqlDataSource dataSource;
    private readonly Func<CustodyScope, CustodyEvent, CancellationToken, Task<CustodyResult>> append;

    /// <summary>The rotation writes through the data source built with the credential of the owner, which is the one allowed to add a label.</summary>
    public DomainLabelRotation(NpgsqlDataSource dataSource)
        : this(dataSource, CustodyAppend.AppendAsync)
    {
    }

    /// <summary>The rotation with the way of adding the event given, so that a test can make the chain answer as it does when another writer was first.</summary>
    internal DomainLabelRotation(NpgsqlDataSource dataSource, Func<CustodyScope, CustodyEvent, CancellationToken, Task<CustodyResult>> append)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(append);
        this.dataSource = dataSource;
        this.append = append;
    }

    /// <summary>The labels of derivation a domain takes in the generation given, one for each purpose, in the order of the purposes.</summary>
    public static IReadOnlyList<string> LabelsOf(int domainId, int version) =>
        Enum.GetValues<LabelPurpose>().Order().Select(purpose => new DerivationLabel(purpose, domainId, version).Text).ToList();

    /// <summary>Passes the domain to the next generation of its labels if the custody holds them; a refusal writes nothing, and a missing label is named.</summary>
    public Task<DomainLabelResult> RotateAsync(int domainId, DerivationLabels custody, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(custody);
        return RunAsync(domainId, custody, cancellationToken);
    }

    private async Task<DomainLabelResult> RunAsync(int domainId, DerivationLabels custody, CancellationToken cancellationToken)
    {
        var answer = new StrongBox<DomainLabelResult?>();
        _ = await CustodyTransaction.RunAsync(dataSource, scope => RotateInScopeAsync(scope, (domainId, custody), answer, cancellationToken), cancellationToken).ConfigureAwait(false);
        return answer.Value ?? new DomainLabelResult { Outcome = DomainLabelOutcome.Conflict };
    }

    private async Task<CustodyResult?> RotateInScopeAsync(CustodyScope scope, (int Domain, DerivationLabels Custody) asked, StrongBox<DomainLabelResult?> answer, CancellationToken cancellationToken)
    {
        var newest = await DomainLabelQueries.NewestVersionAsync(scope, asked.Domain, cancellationToken).ConfigureAwait(false);
        if (newest == 0)
        {
            answer.Value = new DomainLabelResult { Outcome = DomainLabelOutcome.DomainNotFound, DomainId = asked.Domain };
            return null;
        }

        var next = newest + 1;
        var labels = LabelsOf(asked.Domain, next);
        var lacking = asked.Custody.Lacking(labels);
        if (lacking.Count > 0)
        {
            answer.Value = new DomainLabelResult { Outcome = DomainLabelOutcome.LabelsMissing, DomainId = asked.Domain, Version = next, MissingLabels = lacking };
            return null;
        }

        var change = new Change(asked.Domain, next, labels, CustodyEvent.DomainLabelRotated(asked.Domain, next));
        return await WriteAsync(scope, change, answer, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes the labels and then the event, and commits only if the chain took the event.</summary>
    private async Task<CustodyResult?> WriteAsync(CustodyScope scope, Change change, StrongBox<DomainLabelResult?> answer, CancellationToken cancellationToken)
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

        var chain = await append(scope, change.Pending, cancellationToken).ConfigureAwait(false);
        if (chain.Outcome != CustodyOutcome.Appended)
        {
            answer.Value = new DomainLabelResult { Outcome = chain.Outcome == CustodyOutcome.Conflict ? DomainLabelOutcome.Conflict : DomainLabelOutcome.ChainRefused, DomainId = change.Domain };
            return null;
        }

        await scope.CommitAsync(cancellationToken).ConfigureAwait(false);
        answer.Value = new DomainLabelResult { Outcome = DomainLabelOutcome.Rotated, DomainId = change.Domain, Version = change.Version, Sequence = chain.Sequence };
        return chain;
    }

    private sealed record Change(int Domain, int Version, IReadOnlyList<string> Labels, CustodyEvent Pending);
}
