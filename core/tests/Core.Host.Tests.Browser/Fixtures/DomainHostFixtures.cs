using System.Diagnostics.CodeAnalysis;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>The host of the screen of the domains with two domains, started once for the tests of a class and discarded with it.</summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class DomainsHostFixture : IAsyncLifetime
{
    private DomainScreenHost? host;

    internal DomainScreenHost Host => host ?? throw new InvalidOperationException("The host of the domains was not started.");

    public async ValueTask InitializeAsync() => host = await DomainScreenHost.StartAsync(withDomains: true, CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

/// <summary>The host of the screen of the domains with none, started once for the tests of a class and discarded with it.</summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class NoDomainsHostFixture : IAsyncLifetime
{
    private DomainScreenHost? host;

    internal DomainScreenHost Host => host ?? throw new InvalidOperationException("The host with no domain was not started.");

    public async ValueTask InitializeAsync() => host = await DomainScreenHost.StartAsync(withDomains: false, CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}
