using Core.Tests.Properties.Harness;
using CsCheck;

namespace Core.Tests.Properties.Configuration;

/// <summary>A broken load makes its real property fail with a report that prints the sources that failed, not their type name.</summary>
public sealed class CounterexampleReportTests
{
    private static ClosedConfigurationInvariants Over(ConfigurationLoader load) => new(PropertyRunner.FromProcess(), load);

    [Fact]
    public void UnknownKeyReportShowsTheKeyAndTheSources()
    {
        var failure = Assert.Throws<CsCheckException>(Over(BrokenLoaders.IgnoringUnknownKeys).UnknownKeyIsRefusedAndNamed);

        Assert.Contains(" over [", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Sample:Name=", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingKeyReportShowsTheKeyAndTheSources()
    {
        var failure = Assert.Throws<CsCheckException>(Over(BrokenLoaders.FillingMissingKeys).MissingRequiredKeyIsRefusedAndNamed);

        Assert.Contains(" over [", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Sample:", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OriginReportShowsTheSources()
    {
        var failure = Assert.Throws<CsCheckException>(Over(BrokenLoaders.HidingOrigins).LoadedValuesShowTheirOrigin);

        Assert.Contains("Sample:Name=", failure.Message, StringComparison.Ordinal);
    }
}
