using Core.Persistence.Custody.Verification;
using Core.Persistence.People;
using Core.Persistence.Settings;
using Npgsql;

namespace Core.Host.Settings;

/// <summary>What the applier asks of the database before it writes: that the chain closes, read without recording, and that each author is still active with the technical administration.</summary>
internal sealed class ApplicationFactsReader
{
    private readonly CustodyVerifier verifier;
    private readonly PersonRegistry registry;

    /// <summary>The reader over the database of the applier's credential, which only reads.</summary>
    /// <param name="dataSource">The connections of that credential.</param>
    /// <param name="time">The clock the verification reads.</param>
    internal ApplicationFactsReader(NpgsqlDataSource dataSource, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        verifier = new CustodyVerifier(dataSource, time);
        registry = new PersonRegistry(dataSource);
    }

    /// <summary>The facts about the change now, as the database tells them.</summary>
    /// <param name="change">The change approved.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    internal Task<ApplicationFacts> ReadAsync(ApprovedChange change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        return ReadFactsAsync(change, cancellationToken);
    }

    private async Task<ApplicationFacts> ReadFactsAsync(ApprovedChange change, CancellationToken cancellationToken)
    {
        var closed = await verifier.HoldsAsync(cancellationToken).ConfigureAwait(false);
        var submitter = await registry.ActiveFunctionOfAsync(change.SubmitterId, cancellationToken).ConfigureAwait(false);
        var approver = await registry.ActiveFunctionOfAsync(change.ApproverId, cancellationToken).ConfigureAwait(false);
        return new ApplicationFacts(submitter == PersonFunctions.Administration, approver == PersonFunctions.Administration, closed);
    }
}
