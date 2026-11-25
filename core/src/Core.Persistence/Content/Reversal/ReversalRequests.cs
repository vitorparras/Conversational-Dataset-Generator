using Core.Ingestion.Contracts.Reversal;
using Core.Persistence.Content.Sealing;
using Npgsql;

namespace Core.Persistence.Content.Reversal;

/// <summary>
/// The requests to reverse a marker and their authorizations (issues 103 and 104): each act is a row of its own with its event in one serializable transaction, a refusal is an event
/// and no row, an incomplete request writes nothing, and with the chain broken nothing is written, not even the refusal. Why a request asks and what for go sealed in its row.
/// </summary>
public sealed class ReversalRequests
{
    private readonly ReversalAsking asking;
    private readonly ReversalDeciding deciding;

    /// <summary>The requests over the database, sealed through the protected content, at the moments the clock gives.</summary>
    /// <param name="dataSource">The source of the connections, with the application's credential.</param>
    /// <param name="protectedContent">Seals why a request asks and what for.</param>
    /// <param name="time">The clock that says whether an authorization is in force.</param>
    public ReversalRequests(NpgsqlDataSource dataSource, ProtectedContent protectedContent, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(protectedContent);
        ArgumentNullException.ThrowIfNull(time);
        (asking, deciding) = (new ReversalAsking(dataSource, protectedContent), new ReversalDeciding(dataSource, time));
    }

    /// <summary>Asks for the reversal of one marker in one item: written with its event, or refused with an event of its own, or, when incomplete, nothing.</summary>
    /// <param name="domainId">The domain of the item.</param>
    /// <param name="personId">Who asks.</param>
    /// <param name="draft">What the form sent.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<ReversalResult> AskAsync(int domainId, long personId, ReversalRequestDraft draft, CancellationToken cancellationToken) =>
        asking.AskAsync((domainId, personId), draft, cancellationToken);

    /// <summary>Authorizes the request until the moment given, by another person of the curation, once any authorization before expired and never once declined.</summary>
    /// <param name="act">Who authorizes which request of which domain.</param>
    /// <param name="validUntil">Until when the authorization holds.</param>
    /// <param name="cancellationToken">Cancels the authorization.</param>
    public Task<ReversalResult> AuthorizeAsync(ReversalAct act, DateTimeOffset validUntil, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(act);
        return deciding.DecideAsync(act, validUntil, cancellationToken);
    }

    /// <summary>Declines the request, by another person of the curation, unless it was declined or holds an authorization in force.</summary>
    /// <param name="act">Who declines which request of which domain.</param>
    /// <param name="cancellationToken">Cancels the decline.</param>
    public Task<ReversalResult> DeclineAsync(ReversalAct act, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(act);
        return deciding.DecideAsync(act, null, cancellationToken);
    }
}
