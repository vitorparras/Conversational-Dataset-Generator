using System.Diagnostics.CodeAnalysis;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>The host of the pieces of the review by sample, started once for the tests of a class and discarded with it.</summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class SampleReviewHostFixture : IAsyncLifetime
{
    private SampleReviewPieceHost? host;

    internal SampleReviewPieceHost Host => host ?? throw new InvalidOperationException("The host of the review by sample was not started.");

    public async ValueTask InitializeAsync() => host = await SampleReviewPieceHost.StartAsync(CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}
