using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Platform.Keys;
using Npgsql;

namespace Core.Persistence.Domains;

/// <summary>
/// The rotation and the revocation of the label of a domain (decision 0045, issue 56): the owner credential writes the labels of the next generation or the revocation, and the event of the chain,
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
    /// <param name="domainId">The identifier of the domain.</param>
    /// <param name="version">The generation.</param>
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

    /// <summary>Revokes a label: it encrypts nothing new and decrypts what it did. The newest generation not revoked of a purpose is kept, for the domain to encrypt under.</summary>
    /// <param name="label">The text of the label.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public Task<DomainLabelResult> RevokeAsync(string label, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(label);
        return DerivationLabel.TryParse(label, out var parsed)
            ? RunAsync((scope, answer) => RevokeInScopeAsync(scope, parsed, answer, cancellationToken), cancellationToken)
            : Task.FromResult(new DomainLabelResult { Outcome = DomainLabelOutcome.LabelInvalid });
    }

    private static DomainLabelOutcome? Refusal(List<DomainLabelQueries.StoredLabel> stored, DerivationLabel label)
    {
        var found = stored.Find(entry => entry.Label.Text == label.Text);
        if (found is null)
        {
            return DomainLabelOutcome.LabelUnknown;
        }

        // A newer generation of the same purpose that is not revoked keeps the domain with a label to encrypt under.
        var newerKept = stored.Exists(entry => entry.Label.Purpose == label.Purpose && entry.Label.Version > label.Version && !entry.Revoked);
        if (found.Revoked)
        {
            return DomainLabelOutcome.AlreadyRevoked;
        }

        return newerKept ? null : DomainLabelOutcome.LastGeneration;
    }

    private async Task<DomainLabelResult> RunAsync(Func<CustodyScope, Answer, Task<CustodyResult?>> body, CancellationToken cancellationToken)
    {
        var answer = new Answer();
        _ = await CustodyTransaction.RunAsync(dataSource, scope => body(scope, answer), cancellationToken).ConfigureAwait(false);
        return answer.Result ?? new DomainLabelResult { Outcome = DomainLabelOutcome.Conflict };
    }

    private static async Task<CustodyResult?> RotateInScopeAsync(CustodyScope scope, (int Domain, DerivationLabels Custody) request, Answer answer, CancellationToken cancellationToken)
    {
        if (!await DomainLabelQueries.ExistsAsync(scope, request.Domain, cancellationToken).ConfigureAwait(false))
        {
            answer.Result = new DomainLabelResult { Outcome = DomainLabelOutcome.DomainNotFound, DomainId = request.Domain };
            return null;
        }

        var stored = await DomainLabelQueries.LabelsAsync(scope, request.Domain, cancellationToken).ConfigureAwait(false);
        if (stored.Count == 0)
        {
            throw new InvalidOperationException($"The domain {request.Domain} has no derivation label; the registry disagrees with itself.");
        }

        var next = stored.Max(entry => entry.Label.Version) + 1;
        var labels = LabelsOf(request.Domain, next);
        var lacking = request.Custody.Lacking(labels);
        if (lacking.Count > 0)
        {
            answer.Result = new DomainLabelResult { Outcome = DomainLabelOutcome.LabelsMissing, DomainId = request.Domain, Version = next, MissingLabels = lacking };
            return null;
        }

        var change = new Change(request.Domain, next, DomainLabelOutcome.Rotated, labels, CustodyEvent.DomainLabelRotated(request.Domain, next));
        return await WriteAsync(scope, change, answer, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<CustodyResult?> RevokeInScopeAsync(CustodyScope scope, DerivationLabel label, Answer answer, CancellationToken cancellationToken)
    {
        var stored = await DomainLabelQueries.LabelsAsync(scope, label.DomainId, cancellationToken).ConfigureAwait(false);
        if (Refusal(stored, label) is { } refusal)
        {
            answer.Result = new DomainLabelResult { Outcome = refusal, DomainId = label.DomainId, Version = label.Version };
            return null;
        }

        var labels = new string[1];
        labels[0] = label.Text;
        var change = new Change(label.DomainId, label.Version, DomainLabelOutcome.Revoked, labels, CustodyEvent.DomainLabelRevoked(label.DomainId, label.Text));
        return await WriteAsync(scope, change, answer, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes the labels or the revocation of them and then the event, and commits only if the chain took the event.</summary>
    private static async Task<CustodyResult?> WriteAsync(CustodyScope scope, Change change, Answer answer, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var label in change.Labels)
            {
                await (change.Done == DomainLabelOutcome.Rotated
                    ? DomainLabelQueries.AddLabelAsync(scope, label, cancellationToken)
                    : DomainLabelQueries.AddRevocationAsync(scope, label, cancellationToken)).ConfigureAwait(false);
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
            answer.Result = new DomainLabelResult { Outcome = outcome, DomainId = change.Domain, ChainOutcome = chain.Outcome };
            return null;
        }

        await scope.CommitAsync(cancellationToken).ConfigureAwait(false);
        answer.Result = new DomainLabelResult { Outcome = change.Done, DomainId = change.Domain, Version = change.Version, Sequence = chain.Sequence };
        return chain;
    }

    private sealed record Change(int Domain, int Version, DomainLabelOutcome Done, IReadOnlyList<string> Labels, CustodyEvent Pending);

    private sealed class Answer
    {
        internal DomainLabelResult? Result { get; set; }
    }
}
