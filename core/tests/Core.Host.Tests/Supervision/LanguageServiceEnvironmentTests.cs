using Core.Host.Supervision;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using Core.Platform.Startup;

namespace Core.Host.Tests.Supervision;

/// <summary>The executable key is accepted only in the Development environment; outside it the start is refused naming the key, and the manifest is never skipped by configuration.</summary>
public sealed class LanguageServiceEnvironmentTests
{
    private const string Given = "zqx-given-7731";

    private static IReadOnlyDictionary<string, string> WithKey() =>
        ClosedConfiguration.Load(
            new ConfigurationSchema([LanguageServiceConfiguration.Section]),
            new StrictArgumentsSource([$"--LanguageService:Executable={Given}"])).Values;

    private static IReadOnlyDictionary<string, string> WithoutKey() =>
        ClosedConfiguration.Load(new ConfigurationSchema([LanguageServiceConfiguration.Section])).Values;

    [Theory]
    [InlineData("Development", true)]
    [InlineData("development", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OnlyTheDevelopmentEnvironmentIsDevelopment(string? environment, bool expected) =>
        Assert.Equal(expected, LanguageServiceConfiguration.IsDevelopment(environment));

    [Fact]
    public void KeyWrittenInDevelopmentIsAcceptedAndGivesTheExecutable()
    {
        var values = WithKey();

        Assert.Null(LanguageServiceConfiguration.ExecutableRefused(values, development: true));
        Assert.Equal(Given, LanguageServiceConfiguration.GivenExecutable(values, development: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KeyNotWrittenIsNeverRefused(bool development)
    {
        var values = WithoutKey();

        Assert.Null(LanguageServiceConfiguration.ExecutableRefused(values, development));
        Assert.Null(LanguageServiceConfiguration.GivenExecutable(values, development));
    }

    [Fact]
    public void KeyWrittenOutsideDevelopmentIsRefusedNamingTheKey()
    {
        var problem = LanguageServiceConfiguration.ExecutableRefused(WithKey(), development: false);

        Assert.NotNull(problem);
        Assert.Equal(LanguageServiceConfiguration.ExecutablePath, problem.Key);
        Assert.Contains("'LanguageService:Executable'", problem.Describe(), StringComparison.Ordinal);
        Assert.Contains("Development", problem.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void RefusalsAreNoneWhenNeitherTheExecutableNorTheTimeLimitIsRefused() =>
        Assert.Empty(LanguageServiceConfiguration.Refusals(WithoutKey(), TimeSpan.FromSeconds(10)));

    [Fact]
    public void RefusalsHoldTheTimeLimitOutOfRange() =>
        Assert.Equal(
            LanguageServiceConfiguration.AnalyzeTimeoutPath,
            Assert.Single(LanguageServiceConfiguration.Refusals(WithoutKey(), analyzeTimeout: null)).Key);

    [Fact]
    public void KeyWrittenOutsideDevelopmentGivesNoExecutableSoTheFolderIsChecked() =>
        Assert.Null(LanguageServiceConfiguration.GivenExecutable(WithKey(), development: false));

    [Fact]
    public async Task FolderCheckHandedNoExecutableDoesNotSkipTheManifest()
    {
        using var mounted = new MountedServiceFolder();
        File.Delete(mounted.Folder.Manifest);
        var given = LanguageServiceConfiguration.GivenExecutable(WithKey(), development: false);

        var result = await new ServiceFolderCheck(mounted.Folder, given).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
    }

    [Fact]
    public async Task HostOutsideDevelopmentWithTheKeyEndsWithTheRefusalCodeNamingTheKey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var copy = new HostCopy(/*lang=json*/ "{}");
        var installation = copy.Installation($"--LanguageService:Executable={Given}") with
        {
            ProvidesLanguageService = false,
            Environment = new Dictionary<string, string>(StringComparer.Ordinal) { [LanguageServiceConfiguration.EnvironmentVariable] = "Production" },
        };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, cancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'LanguageService:Executable'", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HostOutsideDevelopmentWithoutTheKeyChecksTheFolderInsteadOfRefusingTheConfiguration()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var copy = new HostCopy(/*lang=json*/ "{}");
        var installation = copy.Installation() with
        {
            ProvidesLanguageService = false,
            Environment = new Dictionary<string, string>(StringComparer.Ordinal) { [LanguageServiceConfiguration.EnvironmentVariable] = "Production" },
        };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, cancellationToken);

        Assert.Equal((int)ExitCode.StartupCheckFailed, exit.Code);
        Assert.Contains("- LanguageServiceFolder:", exit.Error, StringComparison.Ordinal);
    }
}
