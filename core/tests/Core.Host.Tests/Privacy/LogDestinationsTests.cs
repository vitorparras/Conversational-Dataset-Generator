using Core.Platform.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Core.Host.Tests.Privacy;

/// <summary>
/// Every destination the composed host writes its log to is one the sweep reads: a provider with no destination
/// reproves the test and is named, and a run without the real service fails instead of skipping (issue #251, CA-3).
/// </summary>
public sealed class LogDestinationsTests
{
    [Fact]
    public void ComposedHostWritesItsLogToTheConsoleAndTheFileOnly() =>
        Assert.Equivalent(new[] { typeof(ConsoleLoggerProvider), typeof(FileLoggerProvider) }, ComposedProviders.TypesOf(_ => { }));

    [Fact]
    public void EveryProviderOfTheComposedHostIsLinkedToASweptDestination() =>
        Assert.Empty(LogDestinations.Unswept(ComposedProviders.TypesOf(_ => { })));

    [Fact]
    public void ProviderBeyondTheSweptOnesReprovesTheCheckAndIsNamed()
    {
        var types = ComposedProviders.TypesOf(logging => logging.AddProvider(new ZqxUnsweptProvider()));

        var unswept = LogDestinations.Unswept(types);

        Assert.Equal(nameof(ZqxUnsweptProvider), Assert.Single(unswept).Split('+')[^1]);
    }

    [Fact]
    public void RealServiceMissingFailsTheTestInsteadOfSkippingIt()
    {
        var failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => RealServiceGuard.Require(null));

        Assert.Contains("uv", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RealServiceResolvedIsHandedOver() =>
        Assert.Equal("ZQX-INTERPRETER", RealServiceGuard.Require("ZQX-INTERPRETER"));

    private sealed class ZqxUnsweptProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => NullLogger.Instance;

        public void Dispose()
        {
        }
    }
}
