using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;
using static Core.Persistence.Content.Rerun.RerunRows;

namespace Core.Persistence.Content.Reversal;

/// <summary>The requests of a domain with where each stands, and why one asks and what for, opened only after the chain recorded who read it.</summary>
public sealed class ReversalRequestReader
{
    private readonly NpgsqlDataSource dataSource;
    private readonly TimeProvider time;
    private readonly ProtectedContentReader reader;

    /// <summary>The requests over the database, opened through the protected content, at the moments the clock gives.</summary>
    /// <param name="dataSource">The source of the connections, with the application's credential.</param>
    /// <param name="protectedContent">Opens why a request asks and what for.</param>
    /// <param name="time">The clock that says whether an authorization is in force.</param>
    public ReversalRequestReader(NpgsqlDataSource dataSource, ProtectedContent protectedContent, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(protectedContent);
        ArgumentNullException.ThrowIfNull(time);
        (this.dataSource, this.time) = (dataSource, time);
        reader = new ProtectedContentReader(dataSource, protectedContent, new CustodyChain(dataSource));
    }

    /// <summary>A request of the domain, with where it stands now; none when the domain holds no such request.</summary>
    /// <param name="domainId">The domain of the request.</param>
    /// <param name="requestId">The request.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<ListedReversal?> ReadAsync(int domainId, long requestId, CancellationToken cancellationToken) =>
        InDomainAsync(domainId, scope => ReversalStanding.ReadAsync(scope, requestId, time.GetUtcNow(), cancellationToken), cancellationToken);

    /// <summary>A page of the requests the listing asks for, the newest first; a page below the first is the first.</summary>
    /// <param name="listing">Whose requests, narrowed how, and which page.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<ReversalPage> ListAsync(ReversalListing listing, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(listing);
        Bound[] narrowed =
        [
            new("now", NpgsqlDbType.TimestampTz, time.GetUtcNow()), new("mine", NpgsqlDbType.Boolean, listing.Mine), new("person", NpgsqlDbType.Bigint, listing.PersonId),
            new("item", NpgsqlDbType.Bigint, listing.ItemId), new("state", NpgsqlDbType.Text, listing.State is { } state ? ReversalStanding.States[(int)state] : null),
            new("size", NpgsqlDbType.Integer, ReversalPage.Size), new("skip", NpgsqlDbType.Integer, (Math.Max(listing.Page, 1) - 1) * ReversalPage.Size),
        ];
        return InDomainAsync(listing.DomainId, async scope =>
        {
            var rows = await RowsAsync(
                scope, (ContentQueries.ReadReversalRequests, narrowed), row => (Listed: ReversalStanding.Listed(row), Total: row.GetInt64(row.FieldCount - 1)), cancellationToken)
                .ConfigureAwait(false);
            return new ReversalPage([.. rows.Select(row => row.Listed)], rows.Count == 0 ? 0 : (int)rows[0].Total);
        }, cancellationToken);
    }

    /// <summary>Why the request asks and what for, opened only after the chain recorded the reading by the person; with the chain broken, nothing is opened.</summary>
    /// <param name="act">Who reads which request of which domain.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<ReversalJustification> ReadTextAsync(ReversalAct act, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(act);
        return ReadTextInAsync(act, cancellationToken);
    }

    private async Task<ReversalJustification> ReadTextInAsync(ReversalAct act, CancellationToken cancellationToken)
    {
        var read = await reader.ReadAsync(new ContentRead(CustodyAuthor.Person(act.PersonId), ContentColumns.ReversalRequestText, act.DomainId, act.RequestId), cancellationToken)
            .ConfigureAwait(false);
        return read.Outcome == ContentReadOutcome.Read ? ReversalStanding.Opened(read.Outcome, read.Text!) : new ReversalJustification(read.Outcome);
    }

    private async Task<T> InDomainAsync<T>(int domainId, Func<CustodyScope, Task<T>> read, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, domainId, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            return await read(transaction.AsCustodyScope()).ConfigureAwait(false);
        }
    }
}
