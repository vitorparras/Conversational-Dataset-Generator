using Core.Host.Supervision;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host.Tests;

/// <summary>
/// The conditions of origin the installation knows (#85): every installation declares them, from the most restrictive to the most permissive, with no default anywhere; a list that is empty
/// in some place, repeats a name or holds what is not a name is refused saying what was found and what is accepted; and the configuration in force shows the list in full, with the origin
/// of the value and the most restrictive marked (CA-4).
/// </summary>
public sealed class AdmissionConfigurationTests
{
    private const string Key = "Ingestion:Conditions";
    private const string Declared = "restrita,intermediaria,permissiva";

    [Fact]
    public void ListNotDeclaredIsRefusedAsMissingAndNamesTheKey()
    {
        var loaded = Loaded(AdmissionConfiguration.Section);

        var problem = Assert.Single(loaded.Problems);
        Assert.Equal((ConfigurationProblemKind.MissingKey, Key), (problem.Kind, problem.Key));
        Assert.Contains($"'{Key}'", problem.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void ListDeclaredIsTheOneTheInstallationRunsWithInTheOrderGiven()
    {
        var loaded = Loaded(AdmissionConfiguration.Section, $"--{Key}=restrita, intermediaria ,permissiva");

        var conditions = AdmissionConfiguration.Conditions(loaded.Values);

        Assert.Empty(AdmissionConfiguration.Refusals(loaded.Values));
        Assert.Equal(["restrita", "intermediaria", "permissiva"], conditions.Names);
        Assert.Equal("restrita", conditions.MostRestrictive);
    }

    [Theory]
    [InlineData("restrita,restrita")]
    [InlineData("restrita,permissiva,restrita")]
    [InlineData("restrita,,permissiva")]
    [InlineData("restrita,")]
    [InlineData(",restrita")]
    [InlineData("Restrita")]
    [InlineData("1restrita")]
    [InlineData("_restrita")]
    [InlineData("com espaco")]
    [InlineData("com-traco")]
    [InlineData("acentuação")]
    public void ListThatRepeatsOrIsEmptyInSomePlaceOrHoldsWhatIsNotANameIsRefusedSayingWhatWasFoundAndWhatIsAccepted(string declared)
    {
        var loaded = Loaded(AdmissionConfiguration.Section, $"--{Key}={declared}");

        var problem = Assert.Single(AdmissionConfiguration.Refusals(loaded.Values));

        Assert.Equal((ConfigurationProblemKind.ValueNotAccepted, Key), (problem.Kind, problem.Key));
        Assert.Contains($"'{declared}'", problem.Describe(), StringComparison.Ordinal);
        Assert.Contains(AdmissionConfiguration.Accepted, problem.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void NameOfSixtyFourCharactersIsRefusedAndOneOfSixtyThreeIsAccepted()
    {
        var accepted = Loaded(AdmissionConfiguration.Section, $"--{Key}={new string('a', 63)}");
        var refused = Loaded(AdmissionConfiguration.Section, $"--{Key}={new string('a', 64)}");

        Assert.Empty(AdmissionConfiguration.Refusals(accepted.Values));
        _ = Assert.Single(AdmissionConfiguration.Refusals(refused.Values));
    }

    [Fact]
    public void ListBlankIsRefusedAsEmptyBeforeAnyJudgmentOfTheNames()
    {
        var loaded = Loaded(AdmissionConfiguration.Section, $"--{Key}= ");

        Assert.Equal(ConfigurationProblemKind.EmptyValue, Assert.Single(loaded.Problems).Kind);
    }

    [Fact]
    public void MigrationModeDoesNotRequireTheListButStillRefusesOneDeclaredThatIsWrong()
    {
        var absent = Loaded(AdmissionConfiguration.SectionWhenMigrating);
        var wrong = Loaded(AdmissionConfiguration.SectionWhenMigrating, $"--{Key}=restrita,restrita");

        Assert.Empty(absent.Problems);
        Assert.Empty(AdmissionConfiguration.Refusals(absent.Values));
        _ = Assert.Single(AdmissionConfiguration.Refusals(wrong.Values));
    }

    [Fact]
    public void TheHostRefusesAWrongListTogetherWithTheOtherRefusals()
    {
        var loaded = ClosedConfiguration.Load(
            new ConfigurationSchema(
                [AdmissionConfiguration.Section, InstallationConfiguration.Section, SafekeepingConfiguration.Section, LanguageServiceConfiguration.Section]),
            new StrictArgumentsSource([$"--{Key}=restrita,restrita", "--Installation:Kind=demonstracao"]));

        var refusals = HostRefusals.Of(loaded.Values, TimeSpan.FromSeconds(1));

        Assert.Contains(refusals, problem => problem.Key == Key);
    }

    [Fact]
    public void ConfigurationInForceShowsTheWholeListWithTheOriginThatWonAndTheMostRestrictiveMarked()
    {
        var loaded = ClosedConfiguration.Load(
            new ConfigurationSchema([AdmissionConfiguration.Section]),
            new EnvironmentSource([KeyValuePair.Create<string, string?>("CDG_Ingestion__Conditions", "livre,restrita")]),
            new StrictArgumentsSource([$"--{Key}={Declared}"]));

        var entry = Assert.Single(loaded.View.Entries);

        Assert.Equal(new EffectiveEntry(Key, Declared, ConfigurationOrigin.Argument, "mais restrita: restrita"), entry);
    }

    [Fact]
    public void ListFromTheEnvironmentIsShownWithTheEnvironmentAsOriginAndItsOwnFirstNameMarked()
    {
        var loaded = ClosedConfiguration.Load(
            new ConfigurationSchema([AdmissionConfiguration.Section]),
            new EnvironmentSource([KeyValuePair.Create<string, string?>("CDG_Ingestion__Conditions", "sigilosa,livre")]));

        var entry = Assert.Single(loaded.View.Entries);

        Assert.Equal(("sigilosa,livre", ConfigurationOrigin.Environment, "mais restrita: sigilosa"), (entry.Value, entry.Origin, entry.Mark));
    }

    [Fact]
    public void KeyThatHasNoNoteAboutItsValueShowsNone()
    {
        var loaded = ClosedConfiguration.Load(new ConfigurationSchema([PasswordConfiguration.Section]), new StrictArgumentsSource(["--Passwords:WorkFactor=7"]));

        Assert.Null(Assert.Single(loaded.View.Entries).Mark);
    }

    [Fact]
    public void TheShippedSettingsFileDoesNotGiveTheList()
    {
        var shipped = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

        Assert.DoesNotContain("Ingestion", shipped, StringComparison.Ordinal);
        Assert.DoesNotContain("Conditions", shipped, StringComparison.Ordinal);
    }

    private static ConfigurationLoadResult Loaded(ConfigurationSection section, params string[] arguments) =>
        ClosedConfiguration.Load(new ConfigurationSchema([section]), new StrictArgumentsSource(arguments));
}
