namespace Core.Host.Supervision;

/// <summary>Where the running service listens and the secret it accepts, read together from the service alive at that moment.</summary>
/// <param name="Port">The loopback port the service told.</param>
/// <param name="Secret">The secret of the start; never written anywhere.</param>
internal sealed record ServiceEndpoint(int Port, string Secret)
{
    /// <summary>The port alone, so that a log or an exception message that formats the endpoint never carries the secret.</summary>
    public override string ToString() => $"porta {Port}";
}
