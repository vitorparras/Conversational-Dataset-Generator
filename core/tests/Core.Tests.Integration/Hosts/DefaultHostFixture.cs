using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Tests.Fixtures;

namespace Core.Tests.Integration.Hosts;

/// <summary>One host in the default configuration, entered once, for the tests of a class that only read what it answers; without Docker no host starts and the tests are skipped.</summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class DefaultHostFixture : IAsyncLifetime
{
    private AuthenticatedHost? started;

    internal AuthenticatedHost Host => started ?? throw new InvalidOperationException("The host was not started.");

    public async ValueTask InitializeAsync()
    {
        if (DockerAvailability.ShouldRun)
        {
            started = await AuthenticatedHost.StartAsync(new HostSetup(), CancellationToken.None);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (started is not null)
        {
            await started.DisposeAsync();
        }
    }
}
