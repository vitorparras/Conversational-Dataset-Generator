using System.Diagnostics.CodeAnalysis;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>The host of the submission and of the batch, started once for the tests of a class and discarded with it.</summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class SubmissionHostFixture : IAsyncLifetime
{
    private ScreenHost? host;

    internal ScreenHost Host => host ?? throw new InvalidOperationException("The host of the submission was not started.");

    public async ValueTask InitializeAsync() => host = await ScreenHost.StartAsync(SubmissionCase.Parts(), CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}
