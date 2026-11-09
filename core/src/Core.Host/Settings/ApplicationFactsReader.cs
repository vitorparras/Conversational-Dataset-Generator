using Core.Persistence.Custody.Verification;
using Core.Persistence.People;
using Core.Persistence.Settings;

namespace Core.Host.Settings;

/// <summary>What the applier asks of the database before it writes: that the chain closes, read without recording, and that each author is still active with the technical administration.</summary>
internal sealed class ApplicationFactsReader
{
    private readonly CustodyVerifier verifier;
    private readonly PersonRegistry registry;

    /// <summary>The reader over the verifier and the registry, both on the applier's credential, which only reads.</summary>
    internal ApplicationFactsReader(CustodyVerifier verifier, PersonRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        ArgumentNullException.ThrowIfNull(registry);
        this.verifier = verifier;
        this.registry = registry;
    }

    /// <summary>The facts about the change now, as the database tells them.</summary>
    /// <param name="change">The change approved.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    internal async Task<ApplicationFacts> ReadAsync(ApprovedChange change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        var closed = await verifier.HoldsAsync(cancellationToken).ConfigureAwait(false);
        var submitter = await registry.ActiveFunctionOfAsync(change.SubmitterId, cancellationToken).ConfigureAwait(false);
        var approver = await registry.ActiveFunctionOfAsync(change.ApproverId, cancellationToken).ConfigureAwait(false);
        return new ApplicationFacts(submitter == PersonFunctions.Administration, approver == PersonFunctions.Administration, closed);
    }
}
