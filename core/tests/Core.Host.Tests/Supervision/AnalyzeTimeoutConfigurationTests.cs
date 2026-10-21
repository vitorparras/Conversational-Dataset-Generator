using Core.Host.Supervision;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using Core.Platform.Startup;

namespace Core.Host.Tests.Supervision;

/// <summary>The time one analysis may take is a whole number of seconds from 1 to 3600, ten unless configured; anything else refuses the configuration before the service starts.</summary>
public sealed class AnalyzeTimeoutConfigurationTests
{
    private static IReadOnlyDictionary<string, string> Loaded(params string[] arguments) =>
        ClosedConfiguration.Load(
            new ConfigurationSchema([LanguageServiceConfiguration.Section]),
            new StrictArgumentsSource(["--LanguageService:Executable=zqx-missing-7731", .. arguments])).Values;

    [Fact]
    public void TimeLimitIsTenSecondsWhenNothingDeclaresIt()
    {
        var values = Loaded();

        Assert.Equal(TimeSpan.FromSeconds(10), LanguageServiceConfiguration.AnalyzeTimeout(values));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(101)]
    [InlineData(3600)]
    public void DeclaredTimeLimitIsTakenInSeconds(int seconds)
    {
        var values = Loaded($"--LanguageService:AnalyzeTimeoutSeconds={seconds}");

        Assert.Equal(TimeSpan.FromSeconds(seconds), LanguageServiceConfiguration.AnalyzeTimeout(values));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("3601")]
    [InlineData("4294968")]
    [InlineData("1.5")]
    [InlineData(" 10")]
    [InlineData("dez")]
    public void TimeLimitOutsideOneToThreeThousandSixHundredSecondsIsNone(string declared)
    {
        var values = Loaded($"--LanguageService:AnalyzeTimeoutSeconds={declared}");

        Assert.Null(LanguageServiceConfiguration.AnalyzeTimeout(values));
    }

    [Fact]
    public void RefusalNamesTheKeyTheValueAndWhatIsAccepted()
    {
        var values = Loaded("--LanguageService:AnalyzeTimeoutSeconds=0");

        var refusal = LanguageServiceConfiguration.AnalyzeTimeoutRefused(values).Describe();

        Assert.Equal(
            "valor não aceito para 'LanguageService:AnalyzeTimeoutSeconds': encontrado '0'; aceitos: número inteiro de segundos de 1 a 3600",
            refusal);
    }

    [Fact]
    public async Task HostWithATimeLimitOfZeroEndsWithTheRefusalCodeBeforeLookingForTheService()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var copy = new HostCopy(/*lang=json*/ "{}");
        var arguments = (string[])["--LanguageService:Executable=zqx-missing-7731", "--LanguageService:AnalyzeTimeoutSeconds=0"];
        var installation = copy.Installation(arguments) with { ProvidesLanguageService = false };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, cancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'LanguageService:AnalyzeTimeoutSeconds'", exit.Error, StringComparison.Ordinal);
    }
}
