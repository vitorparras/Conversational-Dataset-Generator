using System.Runtime.CompilerServices;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Chain.Append;
using Core.Platform.Keys;
using Npgsql;

namespace Core.Persistence.Domains;

/// <summary>
/// The revocation of a label of a domain (decision 0045, issue 56): the owner credential writes the revocation and the event of the chain in one serializable transaction, both or neither.
/// A revoked label encrypts nothing new and decrypts what it did, and is revoked only if a newer generation of its purpose, not revoked, is left to encrypt under.
/// </summary>
public sealed class DomainLabelRevocation
{
    private readonly NpgsqlDataSource dataSource;
    private readonly Func<CustodyScope, CustodyEvent, CancellationToken, Task<CustodyResult>> append;

    /// <summary>The revocation writes through the data source built with the credential of the owner, which is the one allowed to add one.</summary>
    public DomainLabelRevocation(NpgsqlDataSource dataSource)
        : this(dataSource, CustodyAppend.AppendAsync)
    {
    }

    /// <summary>The revocation with the way of adding the event given, so that a test can make the chain answer as it does when another writer was first.</summary>
    internal DomainLabelRevocation(NpgsqlDataSource dataSource, Func<CustodyScope, CustodyEvent, CancellationToken, Task<CustodyResult>> append)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(append);
        this.dataSource = dataSource;
        this.append = append;
    }

    /// <summary>Revokes the label if it is registered, not yet revoked and not the newest of its purpose left; a refusal writes nothing and says why.</summary>
    /// <param name="label">The text of the label.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public Task<DomainLabelResult> RevokeAsync(string label, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(label);
        return RevokeCheckedAsync(label, cancellationToken);
    }

    private async Task<DomainLabelResult> RevokeCheckedAsync(string label, CancellationToken cancellationToken)
    {
        if (!DerivationLabel.TryParse(label, out var parsed))
        {
            return new DomainLabelResult { Outcome = DomainLabelOutcome.LabelInvalid };
        }

        var answer = new StrongBox<DomainLabelResult?>();
        _ = await CustodyTransaction.RunAsync(dataSource, scope => RevokeInScopeAsync(scope, parsed, answer, cancellationToken), cancellationToken).ConfigureAwait(false);
        return answer.Value ?? new DomainLabelResult { Outcome = DomainLabelOutcome.Conflict };
    }

    private async Task<CustodyResult?> RevokeInScopeAsync(CustodyScope scope, DerivationLabel label, StrongBox<DomainLabelResult?> answer, CancellationToken cancellationToken)
    {
        var state = await DomainLabelQueries.StateAsync(scope, label, cancellationToken).ConfigureAwait(false);
        if (state.Refusal() is { } refusal)
        {
            answer.Value = new DomainLabelResult { Outcome = refusal, DomainId = label.DomainId, Version = label.Version };
            return null;
        }

        try
        {
            await DomainLabelQueries.AddRevocationAsync(scope, label.Text, cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException taken) when (taken.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return null;
        }

        return await RecordAsync(scope, label, answer, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CustodyResult?> RecordAsync(CustodyScope scope, DerivationLabel label, StrongBox<DomainLabelResult?> answer, CancellationToken cancellationToken)
    {
        var chain = await append(scope, CustodyEvent.DomainLabelRevoked(label.DomainId, label.Text), cancellationToken).ConfigureAwait(false);
        if (chain.Outcome != CustodyOutcome.Appended)
        {
            answer.Value = new DomainLabelResult { Outcome = chain.Outcome == CustodyOutcome.Conflict ? DomainLabelOutcome.Conflict : DomainLabelOutcome.ChainRefused, DomainId = label.DomainId };
            return null;
        }

        await scope.CommitAsync(cancellationToken).ConfigureAwait(false);
        answer.Value = new DomainLabelResult { Outcome = DomainLabelOutcome.Revoked, DomainId = label.DomainId, Version = label.Version, Sequence = chain.Sequence };
        return chain;
    }
}
