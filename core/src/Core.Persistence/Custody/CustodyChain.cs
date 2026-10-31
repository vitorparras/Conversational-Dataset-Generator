using Npgsql;

namespace Core.Persistence.Custody;

/// <summary>
/// The chain of custody of the installation: events added one after another, each carrying the digest of the one before and its own, both computed by the server.
/// An event is never changed or removed; a correction is an event of its own that points at the one it corrects.
/// </summary>
public sealed class CustodyChain
{
    private readonly NpgsqlDataSource dataSource;

    /// <summary>The chain is written through the data source built with the application's credential.</summary>
    public CustodyChain(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <summary>Adds the event in a serializable transaction; a refusal writes nothing, and a writer that met another at the same link is told so.</summary>
    /// <param name="custodyEvent">The event to add.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public Task<CustodyResult> AppendAsync(CustodyEvent custodyEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(custodyEvent);
        return AddAsync(custodyEvent, cancellationToken);
    }

    private async Task<CustodyResult> AddAsync(CustodyEvent custodyEvent, CancellationToken cancellationToken) =>
        (await CustodyTransaction.AppendAfterAsync(dataSource, _ => Task.FromResult<CustodyEvent?>(custodyEvent), cancellationToken).ConfigureAwait(false))!;
}
