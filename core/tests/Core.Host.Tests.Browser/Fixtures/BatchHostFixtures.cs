using System.Diagnostics.CodeAnalysis;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>The host of the screen of the batches with the first domain granted, started once for the tests of a class and discarded with it.</summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class GrantedBatchesHostFixture : IAsyncLifetime
{
    private ScreenHost? host;

    internal ScreenHost Host => host ?? throw new InvalidOperationException("The host of the batches was not started.");

    public async ValueTask InitializeAsync() => host = await ScreenHost.StartAsync(BatchCase.Parts(granting: true), CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

/// <summary>The host of the screen of the batches with no domain granted, started once for the tests of a class and discarded with it.</summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class UngrantedBatchesHostFixture : IAsyncLifetime
{
    private ScreenHost? host;

    internal ScreenHost Host => host ?? throw new InvalidOperationException("The host with no grant was not started.");

    public async ValueTask InitializeAsync() => host = await ScreenHost.StartAsync(BatchCase.Parts(granting: false), CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}
