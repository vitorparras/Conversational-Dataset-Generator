namespace Core.Host.Endpoints;

/// <summary>The one source of the domains granted to whoever entered. No grant is recorded before #112, so it gives none, and the screens show that, never an error.</summary>
internal static class DomainGrants
{
    /// <summary>No domain: a sequence of none, written so because the analyzer turns an empty array into an expression of collection, which core/src does not take.</summary>
    internal static IEnumerable<string> None { get; } = Enumerable.Repeat(string.Empty, 0);
}
