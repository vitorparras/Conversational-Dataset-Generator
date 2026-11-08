using System.Diagnostics.CodeAnalysis;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>The host of the screens of the chain with the chain intact, started once for the tests of a class and discarded with it.</summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class IntactChainHostFixture : IAsyncLifetime
{
    private ChainScreenHost? host;

    internal ChainScreenHost Host => host ?? throw new InvalidOperationException("The host with the chain intact was not started.");

    public async ValueTask InitializeAsync() => host = await ChainScreenHost.StartAsync(broken: false, CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

/// <summary>The host of the screens of the chain with the second event changed outside the system, started once for the tests of a class and discarded with it.</summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class BrokenChainHostFixture : IAsyncLifetime
{
    private ChainScreenHost? host;

    internal ChainScreenHost Host => host ?? throw new InvalidOperationException("The host with the broken chain was not started.");

    public async ValueTask InitializeAsync() => host = await ChainScreenHost.StartAsync(broken: true, CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}
