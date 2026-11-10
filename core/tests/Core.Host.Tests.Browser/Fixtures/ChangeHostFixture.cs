using System.Diagnostics.CodeAnalysis;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>The host of the screen of the changes of configuration, started once for the tests of a class and discarded with it.</summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class ChangeHostFixture : IAsyncLifetime
{
    private ChangeScreenHost? host;

    internal ChangeScreenHost Host => host ?? throw new InvalidOperationException("The host of the changes was not started.");

    public async ValueTask InitializeAsync() => host = await ChangeScreenHost.StartAsync(CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}
