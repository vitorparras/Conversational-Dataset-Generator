using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;

namespace Core.Platform.Tests.Sources;

/// <summary>Only the variables with the prefix are read, and the double underscore becomes the section separator.</summary>
public sealed class EnvironmentSourceTests
{
    [Fact]
    public void PrefixedVariableIsReadAsPathWithEnvironmentOrigin()
    {
        var source = new EnvironmentSource([Variable("CDG_Sample__Mode", "lenient")]);

        var reading = source.Read();

        Assert.Equal(ConfigurationOrigin.Environment, reading.Origin);
        Assert.Equal([KeyValuePair.Create("Sample:Mode", (string?)"lenient")], reading.Entries);
        Assert.Empty(reading.Problems);
    }

    [Fact]
    public void VariableWithoutThePrefixIsNotRead()
    {
        var source = new EnvironmentSource([Variable("Sample__Mode", "lenient"), Variable("PATH", "x")]);

        var reading = source.Read();

        Assert.Empty(reading.Entries);
    }

    [Fact]
    public void PrefixIsMatchedWhateverItsCase()
    {
        var source = new EnvironmentSource([Variable("cdg_Sample__Mode", "lenient")]);

        var reading = source.Read();

        Assert.Equal("Sample:Mode", Assert.Single(reading.Entries).Key);
    }

    [Fact]
    public void VariablesAreHandedOverInNameOrder()
    {
        var source = new EnvironmentSource([Variable("CDG_B__Key", "2"), Variable("CDG_A__Key", "1")]);

        var reading = source.Read();

        Assert.Equal(["A:Key", "B:Key"], reading.Entries.Select(entry => entry.Key));
    }

    [Fact]
    public void ProcessSourceReadsTheVariablesOfTheRunningProcess()
    {
        using var variable = new ScopedEnvironmentVariable("CDG_ZqxSample__Marker", "7731");

        var reading = EnvironmentSource.FromProcess().Read();

        Assert.Contains(KeyValuePair.Create("ZqxSample:Marker", (string?)"7731"), reading.Entries);
    }

    private static KeyValuePair<string, string?> Variable(string name, string value) => KeyValuePair.Create(name, (string?)value);
}
