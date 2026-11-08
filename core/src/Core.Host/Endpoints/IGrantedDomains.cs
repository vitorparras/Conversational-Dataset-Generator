namespace Core.Host.Endpoints;

/// <summary>The domains granted to a person, by the identifier of each: the one place the routes of the ingestion ask which domains they may touch.</summary>
internal interface IGrantedDomains
{
    /// <summary>The domains granted to the person; none when no grant is recorded.</summary>
    /// <param name="personId">The person of the session.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    Task<IReadOnlyCollection<int>> OfAsync(long personId, CancellationToken cancellationToken);
}
