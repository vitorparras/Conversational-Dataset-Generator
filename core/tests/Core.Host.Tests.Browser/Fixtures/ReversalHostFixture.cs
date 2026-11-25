using System.Diagnostics.CodeAnalysis;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>The host of the screens of the reversal with its two people of the curation, started once for the tests of a class and discarded with it.</summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class ReversalHostFixture : IAsyncLifetime
{
    private ReversalCase? host;

    internal ReversalCase Case => host ?? throw new InvalidOperationException("The host of the reversal was not started.");

    internal ScreenHost Host => Case.Host;

    public async ValueTask InitializeAsync() => host = await ReversalCase.StartAsync(CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}
