using System.Diagnostics.CodeAnalysis;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>The host of the screen of the seal in the manual situation, started once for the tests of a class and discarded with it.</summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class ManualSealHostFixture : IAsyncLifetime
{
    private SealScreenHost? host;

    internal SealScreenHost Host => host ?? throw new InvalidOperationException("The host of the manual seal was not started.");

    public async ValueTask InitializeAsync() => host = await SealScreenHost.StartAsync(manual: true, CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

/// <summary>The host of the screen of the seal where the host sends the seal itself, started once for the tests of a class and discarded with it.</summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class SentSealHostFixture : IAsyncLifetime
{
    private SealScreenHost? host;

    internal SealScreenHost Host => host ?? throw new InvalidOperationException("The host of the sent seal was not started.");

    public async ValueTask InitializeAsync() => host = await SealScreenHost.StartAsync(manual: false, CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}
