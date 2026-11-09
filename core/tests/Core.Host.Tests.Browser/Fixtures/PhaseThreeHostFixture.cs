using System.Diagnostics.CodeAnalysis;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>The host of every screen of phase 3, started once for the tests of a class and discarded with it.</summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class PhaseThreeHostFixture : IAsyncLifetime
{
    private PhaseThreeScreenHost? host;

    internal PhaseThreeScreenHost Host => host ?? throw new InvalidOperationException("The host of the screens of phase 3 was not started.");

    public async ValueTask InitializeAsync() => host = await PhaseThreeScreenHost.StartAsync(CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}
