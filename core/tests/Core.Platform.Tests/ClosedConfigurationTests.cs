using Core.Platform.Configuration;

namespace Core.Platform.Tests;

/// <summary>Loading against a closed schema refuses what it does not know and names it.</summary>
public sealed class ClosedConfigurationTests
{
    private const string Marker = "ZQX-MARKER-7731";

    private static readonly ConfigurationSchema Schema = new(
    [
        new ConfigurationSection(
            "Sample",
            [
                ConfigurationKey.Protection("Threshold"),
                ConfigurationKey.WithDefault("Mode", "strict").Accepting("strict", "lenient"),
            ]),
    ]);

    [Fact]
    public void UnknownKeyIsRefusedAndNamed()
    {
        var source = Source(("Sample:Threshold", "3"), ("Sample:Treshold", "3"));

        var result = ClosedConfiguration.Load(Schema, source);

        var problem = Assert.Single(result.Problems);
        Assert.Equal(ConfigurationProblemKind.UnknownKey, problem.Kind);
        Assert.Equal("Sample:Treshold", problem.Key);
        Assert.Contains("'Sample:Treshold'", problem.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownKeyWrittenWithoutAValueIsStillRefusedAndNamed()
    {
        var source = Source(("Sample:Threshold", "3"), ("Typo", null));

        var result = ClosedConfiguration.Load(Schema, source);

        var problem = Assert.Single(result.Problems);
        Assert.Equal((ConfigurationProblemKind.UnknownKey, "Typo"), (problem.Kind, problem.Key));
    }

    [Fact]
    public void UnknownKeyValueIsNotEchoed()
    {
        var source = Source(("Sample:Threshold", "3"), ("Sample:Tokn", Marker));

        var result = ClosedConfiguration.Load(Schema, source);

        var problem = Assert.Single(result.Problems);
        Assert.Null(problem.Found);
        Assert.DoesNotContain(Marker, problem.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void MissingRequiredKeyIsRefusedAndNamed()
    {
        var source = Source(("Sample:Mode", "lenient"));

        var result = ClosedConfiguration.Load(Schema, source);

        var problem = Assert.Single(result.Problems);
        Assert.Equal(ConfigurationProblemKind.MissingKey, problem.Kind);
        Assert.Equal("Sample:Threshold", problem.Key);
        Assert.Contains("'Sample:Threshold'", problem.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void RefusedLoadCarriesNoValues()
    {
        var source = Source(("Sample:Mode", "lenient"));

        var result = ClosedConfiguration.Load(Schema, source);

        Assert.True(result.Refused);
        Assert.Empty(result.Values);
    }

    [Fact]
    public void ValueOutsideTheAcceptedSetSaysWhatWasFoundAndWhatIsAccepted()
    {
        var source = Source(("Sample:Threshold", "3"), ("Sample:Mode", "loose"));

        var result = ClosedConfiguration.Load(Schema, source);

        var problem = Assert.Single(result.Problems);
        Assert.Equal(ConfigurationProblemKind.ValueNotAccepted, problem.Kind);
        Assert.Equal(("Sample:Mode", "loose"), (problem.Key, problem.Found));
        Assert.Equal(["strict", "lenient"], problem.Accepted);
        Assert.Contains("encontrado 'loose'; aceitos: strict, lenient", problem.Describe(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DeclaredKeyWrittenWithoutAValueIsRefusedAgainstTheSchema(string? value)
    {
        var source = Source(("Sample:Threshold", "3"), ("Sample:Mode", value));

        var result = ClosedConfiguration.Load(Schema, source);

        var problem = Assert.Single(result.Problems);
        Assert.Equal((ConfigurationProblemKind.EmptyValue, "Sample:Mode"), (problem.Kind, problem.Key));
        Assert.Equal(["strict", "lenient"], problem.Accepted);
        Assert.Contains("'Sample:Mode' declarada sem valor", problem.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void RefusedSensitiveValueIsNotEchoed()
    {
        var schema = new ConfigurationSchema(
            [new ConfigurationSection("Sample", [ConfigurationKey.Required("Token").Accepting("a").AsSensitive()])]);
        var source = Source(("Sample:Token", Marker));

        var result = ClosedConfiguration.Load(schema, source);

        var problem = Assert.Single(result.Problems);
        Assert.Equal(ConfigurationProblemKind.ValueNotAccepted, problem.Kind);
        Assert.Null(problem.Found);
        Assert.DoesNotContain(Marker, problem.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void KeyNoSourceWritesTakesItsDefault()
    {
        var source = Source(("Sample:Threshold", "3"));

        var result = ClosedConfiguration.Load(Schema, source);

        Assert.Equal("strict", result.Values["Sample:Mode"]);
    }

    [Fact]
    public void SuccessfulLoadReturnsEveryValue()
    {
        var source = Source(("Sample:Threshold", "3"), ("Sample:Mode", "lenient"));

        var result = ClosedConfiguration.Load(Schema, source);

        Assert.False(result.Refused);
        Assert.Empty(result.Problems);
        Assert.Equal(
            new Dictionary<string, string> { ["Sample:Threshold"] = "3", ["Sample:Mode"] = "lenient" },
            result.Values);
    }

    [Fact]
    public void KeyWrittenInAnotherCaseIsFoundAndReportedUnderItsDeclaredPath()
    {
        var source = Source(("sample:THRESHOLD", "3"));

        var result = ClosedConfiguration.Load(Schema, source);

        Assert.Empty(result.Problems);
        Assert.Contains("Sample:Threshold", result.Values.Keys);
    }

    [Fact]
    public void EveryProblemIsReportedNotOnlyTheFirst()
    {
        var source = Source(("Typo", "1"), ("Sample:Mode", "loose"));

        var result = ClosedConfiguration.Load(Schema, source);

        Assert.Equal(
            [ConfigurationProblemKind.UnknownKey, ConfigurationProblemKind.MissingKey, ConfigurationProblemKind.ValueNotAccepted],
            result.Problems.Select(problem => problem.Kind));
    }

    [Fact]
    public void KeyWrittenByTwoSourcesTakesTheValueAndOriginOfTheHigherPrecedence()
    {
        var file = Source(ConfigurationOrigin.File, ("Sample:Threshold", "3"), ("Sample:Mode", "strict"));
        var environment = Source(ConfigurationOrigin.Environment, ("Sample:Mode", "lenient"));

        var result = ClosedConfiguration.Load(Schema, file, environment);

        Assert.Equal("lenient", result.Values["Sample:Mode"]);
        Assert.Equal(
            new EffectiveEntry("Sample:Mode", "lenient", ConfigurationOrigin.Environment),
            result.View.Entries.Single(entry => entry.Key == "Sample:Mode"));
    }

    [Fact]
    public void PrecedenceIsFileBelowEnvironmentBelowArgumentWhateverTheOrderGiven()
    {
        var argument = Source(ConfigurationOrigin.Argument, ("Sample:Threshold", "1"));
        var file = Source(ConfigurationOrigin.File, ("Sample:Threshold", "3"));
        var environment = Source(ConfigurationOrigin.Environment, ("Sample:Threshold", "2"));

        var result = ClosedConfiguration.Load(Schema, argument, file, environment);

        Assert.Equal(
            new EffectiveEntry("Sample:Threshold", "1", ConfigurationOrigin.Argument),
            result.View.Entries.Single(entry => entry.Key == "Sample:Threshold"));
    }

    [Fact]
    public void EnvironmentOverridesTheFileWhenNoArgumentWritesTheKey()
    {
        var file = Source(ConfigurationOrigin.File, ("Sample:Threshold", "3"));
        var environment = Source(ConfigurationOrigin.Environment, ("Sample:Threshold", "2"));

        var result = ClosedConfiguration.Load(Schema, environment, file);

        Assert.Equal(
            new EffectiveEntry("Sample:Threshold", "2", ConfigurationOrigin.Environment),
            result.View.Entries.Single(entry => entry.Key == "Sample:Threshold"));
    }

    [Fact]
    public void KeyNoSourceWritesIsShownWithTheDefaultOrigin()
    {
        var source = Source(("Sample:Threshold", "3"));

        var result = ClosedConfiguration.Load(Schema, source);

        Assert.Equal(
            [
                new EffectiveEntry("Sample:Threshold", "3", ConfigurationOrigin.File),
                new EffectiveEntry("Sample:Mode", "strict", ConfigurationOrigin.Default),
            ],
            result.View.Entries);
    }

    [Fact]
    public void KeyWrittenTwiceByTheSameSourceIsRefusedAndNamed()
    {
        var source = Source(("Sample:Threshold", "3"), ("Sample:Mode", "strict"), ("sample:mode", "lenient"));

        var result = ClosedConfiguration.Load(Schema, source);

        var problem = Assert.Single(result.Problems);
        Assert.Equal((ConfigurationProblemKind.RepeatedKey, "Sample:Mode"), (problem.Kind, problem.Key));
        Assert.Contains("'Sample:Mode' escrita mais de uma vez", problem.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void ProblemASourceMetReadingRefusesTheConfiguration()
    {
        var unreadable = new ConfigurationProblem(ConfigurationProblemKind.UnreadableFile, "settings.json", null, null);
        var file = new FakeConfigurationSource(ConfigurationOrigin.File, unreadable);
        var argument = Source(ConfigurationOrigin.Argument, ("Sample:Threshold", "3"));

        var result = ClosedConfiguration.Load(Schema, file, argument);

        Assert.Equal([unreadable], result.Problems);
        Assert.Contains("'settings.json'", unreadable.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void SensitiveValueIsRedactedInTheViewAndKeptForTheConsumer()
    {
        var schema = new ConfigurationSchema(
            [new ConfigurationSection("Sample", [ConfigurationKey.Required("Token").AsSensitive()])]);
        var source = Source(("Sample:Token", Marker));

        var result = ClosedConfiguration.Load(schema, source);

        Assert.Equal(Marker, result.Values["Sample:Token"]);
        Assert.Equal(
            [new EffectiveEntry("Sample:Token", EffectiveConfiguration.Redacted, ConfigurationOrigin.File)],
            result.View.Entries);
    }

    [Fact]
    public void RefusedLoadHasAnEmptyView()
    {
        var source = Source(("Sample:Mode", "lenient"));

        var result = ClosedConfiguration.Load(Schema, source);

        Assert.Empty(result.View.Entries);
    }

    private static FakeConfigurationSource Source(params (string Key, string? Value)[] entries) =>
        Source(ConfigurationOrigin.File, entries);

    private static FakeConfigurationSource Source(ConfigurationOrigin origin, params (string Key, string? Value)[] entries) =>
        new(entries.Select(entry => KeyValuePair.Create(entry.Key, entry.Value)), origin);
}
