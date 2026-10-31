using Core.Host.Supervision;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;

namespace Core.Host.Tests;

/// <summary>
/// The work factor of the derivation of passwords (#69): every installation declares it, it has no default anywhere,
/// and a value that is not a whole number above zero is refused naming the key.
/// </summary>
public sealed class PasswordConfigurationTests
{
    private const string Key = "Passwords:WorkFactor";

    [Fact]
    public void FactorNotDeclaredIsRefusedAsMissingAndNamesTheKey()
    {
        var loaded = Loaded();

        var problem = Assert.Single(loaded.Problems);
        Assert.Equal(ConfigurationProblemKind.MissingKey, problem.Kind);
        Assert.Equal(Key, problem.Key);
        Assert.Contains($"'{Key}'", problem.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void FactorDeclaredIsTheOneTheDerivationRunsWith()
    {
        var loaded = Loaded($"--{Key}=7");

        Assert.Empty(PasswordConfiguration.Refusals(loaded.Values));
        Assert.Equal(7, PasswordConfiguration.Derivation(loaded.Values).WorkFactor);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("zqx")]
    [InlineData("+3")]
    [InlineData("3 ")]
    [InlineData("99999999999")]
    public void FactorThatIsNotAWholeNumberAboveZeroIsRefusedAndNamesTheKeyAndWhatWasFound(string declared)
    {
        var loaded = Loaded($"--{Key}={declared}");

        var problem = Assert.Single(PasswordConfiguration.Refusals(loaded.Values));
        Assert.Equal(ConfigurationProblemKind.ValueNotAccepted, problem.Kind);
        Assert.Equal(Key, problem.Key);
        Assert.Contains($"'{declared}'", problem.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void FactorDeclaredWithoutValueIsRefused()
    {
        var loaded = Loaded($"--{Key}=");

        var problem = Assert.Single(loaded.Problems);
        Assert.Equal(ConfigurationProblemKind.EmptyValue, problem.Kind);
        Assert.Equal(Key, problem.Key);
    }

    [Fact]
    public void FactorIsNotRequiredByTheMigrationModeThatDerivesNoPassword()
    {
        var loaded = ClosedConfiguration.Load(new ConfigurationSchema([PasswordConfiguration.SectionWhenMigrating]), new StrictArgumentsSource([]));

        Assert.Empty(loaded.Problems);
    }

    [Fact]
    public void TheHostRefusesAFactorThatIsNotPositiveTogetherWithTheOtherRefusals()
    {
        var loaded = ClosedConfiguration.Load(
            new ConfigurationSchema(
                [PasswordConfiguration.Section, InstallationConfiguration.Section, SafekeepingConfiguration.Section, LanguageServiceConfiguration.Section]),
            new StrictArgumentsSource([$"--{Key}=0", "--Installation:Kind=demonstracao"]));

        var refusals = HostConfiguration.Refusals(loaded.Values, TimeSpan.FromSeconds(1));

        Assert.Contains(refusals, problem => problem.Key == Key);
    }

    [Fact]
    public void TheShippedSettingsFileDoesNotGiveAFactor()
    {
        var shipped = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

        Assert.DoesNotContain("Passwords", shipped, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkFactor", shipped, StringComparison.Ordinal);
    }

    private static ConfigurationLoadResult Loaded(params string[] arguments) =>
        ClosedConfiguration.Load(
            new ConfigurationSchema([PasswordConfiguration.Section]),
            new StrictArgumentsSource(arguments));
}
