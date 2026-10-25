using Core.Platform.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Core.Host.Tests.Privacy;

/// <summary>
/// Every destination either logging container of the composed host writes to is one the sweep reads: a provider with no
/// destination reproves the test and is named, and a run without the real service fails instead of skipping (issue #251, CA-3).
/// </summary>
public sealed class LogDestinationsTests
{
    private static readonly Type[] Swept = [typeof(ConsoleLoggerProvider), typeof(FileLoggerProvider)];

    [Fact]
    public void WebContainerWritesItsLogToTheConsoleAndTheFileOnly() =>
        Assert.Equivalent(Swept, ComposedProviders.WebTypesOf(_ => { }));

    [Fact]
    public void StartupContainerWritesItsLogToTheConsoleAndTheFileOnly() =>
        Assert.Equivalent(Swept, ComposedProviders.StartupTypes());

    [Fact]
    public void EveryProviderOfBothContainersIsLinkedToASweptDestination() =>
        Assert.Empty(LogDestinations.Unswept([.. ComposedProviders.WebTypesOf(_ => { }), .. ComposedProviders.StartupTypes()]));

    [Fact]
    public void ProviderBeyondTheSweptOnesInTheWebContainerReprovesTheCheckAndIsNamed()
    {
        var types = ComposedProviders.WebTypesOf(logging => logging.AddProvider(new ZqxUnsweptProvider()));

        Assert.Equal(nameof(ZqxUnsweptProvider), Assert.Single(LogDestinations.Unswept(types)).Split('+')[^1]);
    }

    [Fact]
    public void ProviderBeyondTheSweptOnesInTheStartupContainerReprovesTheCheckAndIsNamed()
    {
        var types = ComposedProviders.StartupTypesWith(logging => logging.AddProvider(new ZqxUnsweptProvider()));

        Assert.Equal(nameof(ZqxUnsweptProvider), Assert.Single(LogDestinations.Unswept(types)).Split('+')[^1]);
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
