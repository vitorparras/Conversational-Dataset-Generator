using Core.Platform.Configuration;

namespace Core.Platform.Tests;

/// <summary>A key says how it must be declared; a protection never carries a default.</summary>
public sealed class ConfigurationKeyTests
{
    [Fact]
    public void ProtectionKeyIsRequiredAndHasNoDefault()
    {
        var key = ConfigurationKey.Protection("Threshold");

        Assert.Equal(KeyObligation.Protection, key.Obligation);
        Assert.True(key.IsRequired);
        Assert.Null(key.DefaultValue);
    }

    [Fact]
    public void DefaultOutsideTheAcceptedSetIsADefect()
    {
        var key = ConfigurationKey.WithDefault("Mode", "loose");

        var defect = Assert.Throws<ArgumentException>(() => key.Accepting("strict", "lenient"));

        Assert.Contains("'Mode'", defect.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyNameWithThePathSeparatorIsADefect()
    {
        var defect = Assert.Throws<ArgumentException>(() => ConfigurationKey.Required("Sample:Mode"));

        Assert.Contains("':'", defect.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData]
    [InlineData("strict", "")]
    [InlineData("strict", "   ")]
    public void EmptyAcceptedSetOrBlankAcceptedValueIsADefect(params string[] values)
    {
        var key = ConfigurationKey.Required("Mode");

        var defect = Assert.Throws<ArgumentException>(() => key.Accepting(values));

        Assert.Contains("accepted set", defect.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyWithoutAnAcceptedSetAcceptsAnyText()
    {
        var key = ConfigurationKey.Required("Threshold");

        Assert.True(key.Accepts("anything"));
    }
}
