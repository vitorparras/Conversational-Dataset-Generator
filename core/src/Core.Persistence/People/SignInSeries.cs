namespace Core.Persistence.People;

/// <summary>The refusals from one network prefix within the window of the series: the prefix, how many there were, and the moment of the first and of the last.</summary>
/// <param name="Network">The prefix, /24 of IPv4 or /48 of IPv6.</param>
/// <param name="Refusals">How many attempts from that prefix were refused within the window.</param>
/// <param name="First">The moment of the first of them.</param>
/// <param name="Last">The moment of the last of them.</param>
public sealed record SignInSeries(string Network, int Refusals, DateTimeOffset First, DateTimeOffset Last);
