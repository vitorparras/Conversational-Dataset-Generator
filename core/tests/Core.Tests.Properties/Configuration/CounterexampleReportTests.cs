using Core.Tests.Properties.Harness;
using CsCheck;

namespace Core.Tests.Properties.Configuration;

/// <summary>A failing property over sources prints the sources, not their type name.</summary>
public sealed class CounterexampleReportTests
{
    [Fact]
    public void FailureReportShowsTheEntriesOfTheSourcesThatFailed()
    {
        var runner = PropertyRunner.FromProcess();

        var failure = Assert.Throws<CsCheckException>(() => runner.Check(ConfigurationGenerators.WellFormed, Assert.Empty));

        Assert.Contains("Sample:Name=", failure.Message, StringComparison.Ordinal);
    }
}
