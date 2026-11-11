using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;

namespace Core.Host.Tests;

/// <summary>
/// The size of the sample of the review is the installation's (issue 83, CA-1): declared in the configuration in force with its origin, a whole number from one on,
/// refused saying what was found and what is accepted when it is anything else, and absent when the installation does not declare it (the per-domain size is for a later phase).
/// </summary>
public sealed class ReviewSampleSizeConfigurationTests
{
    private const string Key = "Ingestion:ReviewSampleSize";
    private const string Conditions = "--Ingestion:Conditions=restrita,permissiva";

    [Theory]
    [InlineData("1", 1)]
    [InlineData("25", 25)]
    [InlineData("007", 7)]
    public void TheSizeDeclaredIsTheOneTheServiceReadsAndHasItsOriginInTheView(string declared, int size)
    {
        var loaded = Loaded($"--{Key}={declared}");

        Assert.Equal(size, AdmissionConfiguration.ReviewSampleSize(loaded.Values));
        Assert.Empty(AdmissionConfiguration.Refusals(loaded.Values));
        Assert.Contains(loaded.View.Entries, entry => entry.Key == Key && entry.Origin == ConfigurationOrigin.Argument);
    }

    [Fact]
    public void ASizeNotDeclaredIsNoProblemAndTheServiceReadsNone()
    {
        var loaded = Loaded();

        Assert.Null(AdmissionConfiguration.ReviewSampleSize(loaded.Values));
        Assert.Empty(AdmissionConfiguration.Refusals(loaded.Values));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("1.5")]
    [InlineData("dez")]
    [InlineData("+2")]
    [InlineData("99999999999")]
    public void ASizeThatIsNotAWholeNumberFromOneOnIsRefusedSayingWhatIsAccepted(string declared)
    {
        var loaded = Loaded($"--{Key}={declared}");

        var problem = Assert.Single(AdmissionConfiguration.Refusals(loaded.Values));

        Assert.Equal((ConfigurationProblemKind.ValueNotAccepted, Key, declared), (problem.Kind, problem.Key, problem.Found));
        Assert.Null(AdmissionConfiguration.ReviewSampleSize(loaded.Values));
    }

    private static ConfigurationLoadResult Loaded(params string[] arguments) =>
        ClosedConfiguration.Load(new ConfigurationSchema([AdmissionConfiguration.Section]), new StrictArgumentsSource([Conditions, .. arguments]));
}
