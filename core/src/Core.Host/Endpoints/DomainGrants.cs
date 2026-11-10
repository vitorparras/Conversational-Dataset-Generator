namespace Core.Host.Endpoints;

/// <summary>
/// The one source of the domains granted to whoever entered, registered as a service so that the real source can take the place of the empty one without a screen or the gate changing (#112).
/// The gate asks it once for the request, with the person of the session. A test host registers its own and fills it.
/// </summary>
internal abstract class DomainGrants
{
    /// <summary>No domain: a sequence of none, written so because the analyzer turns an empty array into an expression of collection, which core/src does not take.</summary>
    internal static IEnumerable<string> None { get; } = Enumerable.Repeat(string.Empty, 0);

    /// <summary>The names of the domains granted to the person, in the order the selector lists them; the first is the active one until the person chooses another.</summary>
    /// <param name="entered">The identifier of the one who entered, the person of the session.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    internal abstract Task<IEnumerable<string>> GrantedToAsync(long entered, CancellationToken cancellationToken);
}
