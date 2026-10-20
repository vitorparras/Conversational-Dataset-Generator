using Core.Platform.Configuration;

namespace Core.Platform.Tests;

/// <summary>An optional key without a default has no value when absent, is still shown, and is judged like any other when written.</summary>
public sealed class OptionalKeyTests
{
    private static readonly ConfigurationSchema Schema = new(
        [new ConfigurationSection("Sample", [ConfigurationKey.Optional("Destination")])]);

    [Fact]
    public void OptionalKeyIsNotRequiredAndHasNoDefault()
    {
        var key = ConfigurationKey.Optional("Destination");

        Assert.False(key.IsRequired);
        Assert.Null(key.DefaultValue);
    }

    [Fact]
    public void AbsentOptionalKeyLoadsWithoutAValueForTheConsumer()
    {
        var result = ClosedConfiguration.Load(Schema);

        Assert.False(result.Refused);
        Assert.False(result.Values.ContainsKey("Sample:Destination"));
    }

    [Fact]
    public void AbsentOptionalKeyIsShownWithoutAValueUnderTheDefaultOrigin()
    {
        var result = ClosedConfiguration.Load(Schema);

        Assert.Equal([new EffectiveEntry("Sample:Destination", Value: null, ConfigurationOrigin.Default)], result.View.Entries);
    }

    [Fact]
    public void WrittenOptionalKeyTakesTheValueAndItsOrigin()
    {
        var source = new FakeConfigurationSource([KeyValuePair.Create<string, string?>("Sample:Destination", "ZQX-VALUE-7731")], ConfigurationOrigin.Argument);

        var result = ClosedConfiguration.Load(Schema, source);

        Assert.Equal("ZQX-VALUE-7731", result.Values["Sample:Destination"]);
        Assert.Equal([new EffectiveEntry("Sample:Destination", "ZQX-VALUE-7731", ConfigurationOrigin.Argument)], result.View.Entries);
    }

    [Fact]
    public void OptionalKeyWrittenEmptyIsRefusedLikeAnyOther()
    {
        var source = new FakeConfigurationSource([KeyValuePair.Create<string, string?>("Sample:Destination", "")], ConfigurationOrigin.File);

        var result = ClosedConfiguration.Load(Schema, source);

        var problem = Assert.Single(result.Problems);
        Assert.Equal((ConfigurationProblemKind.EmptyValue, "Sample:Destination"), (problem.Kind, problem.Key));
    }
}
