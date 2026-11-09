using System.Diagnostics.CodeAnalysis;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>The host of the notices over a chain that conforms, started once for the tests of a class and discarded with it.</summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class SoundNoticeHostFixture : IAsyncLifetime
{
    private NoticeScreenHost? host;

    internal NoticeScreenHost Host => host ?? throw new InvalidOperationException("The host of the notices was not started.");

    public async ValueTask InitializeAsync() => host = await NoticeScreenHost.StartAsync(broken: false, CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

/// <summary>The host of the notices over a chain with the second event changed outside the system, started once for the tests of a class and discarded with it.</summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class BrokenNoticeHostFixture : IAsyncLifetime
{
    private NoticeScreenHost? host;

    internal NoticeScreenHost Host => host ?? throw new InvalidOperationException("The host of the notices was not started.");

    public async ValueTask InitializeAsync() => host = await NoticeScreenHost.StartAsync(broken: true, CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}
