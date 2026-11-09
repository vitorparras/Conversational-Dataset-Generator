using System.Diagnostics.CodeAnalysis;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>The host of the pieces of the detail of a batch, started once for the tests of a class and discarded with it.</summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class BatchDetailHostFixture : IAsyncLifetime
{
    private BatchDetailPieceHost? host;

    internal BatchDetailPieceHost Host => host ?? throw new InvalidOperationException("The host of the detail of the batch was not started.");

    public async ValueTask InitializeAsync() => host = await BatchDetailPieceHost.StartAsync(CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}
