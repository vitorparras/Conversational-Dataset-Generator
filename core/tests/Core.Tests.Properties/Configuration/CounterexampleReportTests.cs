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

        Assert.Matches(@"^\S+ over \[(File|Environment|Argument)\[Sample:Name=", FailedSample.CounterexampleIn(failure.Message));
    }

    [Fact]
    public void MissingKeyReportShowsTheKeyAndTheSources()
    {
        var failure = Assert.Throws<CsCheckException>(Over(BrokenLoaders.FillingMissingKeys).MissingRequiredKeyIsRefusedAndNamed);

        Assert.Matches(@"^\S+ over \[(File|Environment|Argument)\[Sample:(Name|Threshold)=", FailedSample.CounterexampleIn(failure.Message));
    }

    [Fact]
    public void OriginReportShowsTheSources()
    {
        var failure = Assert.Throws<CsCheckException>(Over(BrokenLoaders.HidingOrigins).LoadedValuesShowTheirOrigin);

        Assert.Matches(@"^\[(File|Environment|Argument)\[Sample:Name=", FailedSample.CounterexampleIn(failure.Message));
    }
}
