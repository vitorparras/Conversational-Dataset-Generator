using System.Diagnostics.CodeAnalysis;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>The host of the screens of the own account, started once for the tests of a class and discarded with it.</summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class AccountHostFixture : IAsyncLifetime
{
    private AccountScreenHost? host;

    internal AccountScreenHost Host => host ?? throw new InvalidOperationException("The host of the account was not started.");

    public async ValueTask InitializeAsync() => host = await AccountScreenHost.StartAsync(CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}
