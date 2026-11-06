using System.Diagnostics.CodeAnalysis;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>The host of the entry with one person registered, started once for the tests of a class and discarded with it.</summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class EntryHostFixture : IAsyncLifetime
{
    private EntryScreenHost? host;

    internal EntryScreenHost Host => host ?? throw new InvalidOperationException("The host of the entry was not started.");

    public async ValueTask InitializeAsync() => host = await EntryScreenHost.StartAsync(withPerson: true, CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}
