using Core.Host.Supervision;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;

namespace Core.Host.Tests;

/// <summary>
/// The work factor of the derivation of passwords (#69): every installation declares it, it has no default anywhere,
/// and a value outside the interval of the installation is refused naming the key and the limit crossed (#494, decision 0047): a real installation takes
/// from 600000 to 10000000, a demonstration from 1 to 10000000.
/// </summary>
public sealed class PasswordConfigurationTests
{
    private const string Key = "Passwords:WorkFactor";
    private const string Demonstration = "--Installation:Kind=demonstracao";
    private const string Real = "--Installation:Kind=real";

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
        var loaded = Loaded($"--{Key}=7", Demonstration);

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

    [Theory]
    [InlineData(Real, "600000")]
    [InlineData(Real, "10000000")]
    [InlineData(Real, "1000000")]
    [InlineData(Demonstration, "1")]
    [InlineData(Demonstration, "600000")]
    [InlineData(Demonstration, "10000000")]
    public void FactorOnOrInsideTheLimitsOfTheInstallationIsAccepted(string kind, string declared)
    {
        var loaded = Loaded($"--{Key}={declared}", kind);

        Assert.Empty(PasswordConfiguration.Refusals(loaded.Values));
    }

    [Theory]
    [InlineData(Real)]
    [InlineData("")]
    public void FactorBelowTheFloorOfARealInstallationIsRefusedNamingTheKeyTheMinimumAndTheKindOfInstallation(string kind)
    {
        var loaded = Loaded($"--{Key}=599999", kind);

        var problem = Assert.Single(PasswordConfiguration.Refusals(loaded.Values));
        var text = problem.Describe();
        Assert.Equal((ConfigurationProblemKind.ValueNotAccepted, Key), (problem.Kind, problem.Key));
        Assert.Contains("'599999'", text, StringComparison.Ordinal);
        Assert.Contains("no mínimo 600000 na instalação real", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Real)]
    [InlineData(Demonstration)]
    public void FactorAboveTheCeilingIsRefusedNamingTheKeyAndTheMaximumInEveryKindOfInstallation(string kind)
    {
        var loaded = Loaded($"--{Key}=10000001", kind);

        var problem = Assert.Single(PasswordConfiguration.Refusals(loaded.Values));
        Assert.Equal(Key, problem.Key);
        Assert.Contains("no máximo 10000000", problem.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void FactorOfZeroIsRefusedInADemonstrationNamingTheMinimumOfOne()
    {
        var loaded = Loaded($"--{Key}=0", Demonstration);

        var problem = Assert.Single(PasswordConfiguration.Refusals(loaded.Values));
        Assert.Contains("no mínimo 1", problem.Describe(), StringComparison.Ordinal);
        Assert.DoesNotContain("instalação real", problem.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void TheThreeLimitsAreTheOnesTheDecisionRegisters() =>
        Assert.Equal((600_000, 1, 10_000_000), (PasswordConfiguration.RealFloor, PasswordConfiguration.DemonstrationFloor, PasswordConfiguration.Ceiling));

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
    public void FactorNotDeclaredWhereItIsNotRequiredIsNoRefusal() =>
        Assert.Empty(PasswordConfiguration.Refusals(new Dictionary<string, string>()));

    [Fact]
    public void FactorDeclaredAndInvalidIsRefusedEvenWhereItIsNotRequired()
    {
        var loaded = ClosedConfiguration.Load(new ConfigurationSchema([PasswordConfiguration.SectionWhenMigrating]), new StrictArgumentsSource([$"--{Key}=zqx"]));

        var problem = Assert.Single(PasswordConfiguration.Refusals(loaded.Values));
        Assert.Equal(Key, problem.Key);
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
            new ConfigurationSchema([PasswordConfiguration.Section, InstallationConfiguration.SectionWhenMigrating]),
            new StrictArgumentsSource([.. arguments.Where(argument => argument.Length > 0)]));
}
