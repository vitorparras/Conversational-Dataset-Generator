using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;

namespace Core.Platform.Tests.Sources;

/// <summary>Arguments are read in one exact form; anything else is reported, never thrown.</summary>
public sealed class StrictArgumentsSourceTests
{
    private const string Marker = "ZQX-MARKER-7731";

    [Fact]
    public void WellFormedArgumentIsReadAsKeyAndValueWithArgumentOrigin()
    {
        var source = new StrictArgumentsSource(["--Sample:Mode=lenient"]);

        var reading = source.Read();

        Assert.Equal(ConfigurationOrigin.Argument, reading.Origin);
        Assert.Equal([KeyValuePair.Create("Sample:Mode", (string?)"lenient")], reading.Entries);
        Assert.Empty(reading.Problems);
    }

    [Fact]
    public void ValueMayContainTheAssignmentSign()
    {
        var source = new StrictArgumentsSource(["--Sample:Url=http://127.0.0.1:1?a=b"]);

        var reading = source.Read();

        Assert.Equal("http://127.0.0.1:1?a=b", Assert.Single(reading.Entries).Value);
    }

    [Fact]
    public void EmptyValueIsHandedOverAsWrittenForTheSchemaToJudge()
    {
        var source = new StrictArgumentsSource(["--Sample:Mode="]);

        var reading = source.Read();

        Assert.Equal(KeyValuePair.Create("Sample:Mode", (string?)string.Empty), Assert.Single(reading.Entries));
    }

    [Theory]
    [InlineData("--Sample:Mode", "--Sample:Mode")]
    [InlineData("-Sample:Mode=lenient", "-Sample:Mode")]
    [InlineData("Sample:Mode=lenient", "Sample:Mode")]
    [InlineData("--=lenient", "--")]
    [InlineData("lenient", "lenient")]
    public void MalformedArgumentIsAProblemNamingItNotAnException(string argument, string named)
    {
        var source = new StrictArgumentsSource([argument]);

        var reading = source.Read();

        Assert.Empty(reading.Entries);
        var problem = Assert.Single(reading.Problems);
        Assert.Equal((ConfigurationProblemKind.MalformedArgument, named), (problem.Kind, problem.Key));
        Assert.Contains($"argumento malformado '{named}'", problem.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void MalformedArgumentValueIsNotEchoed()
    {
        var source = new StrictArgumentsSource([$"Sample:Token={Marker}"]);

        var problem = Assert.Single(source.Read().Problems);

        Assert.DoesNotContain(Marker, problem.Key, StringComparison.Ordinal);
        Assert.DoesNotContain(Marker, problem.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void WellFormedAndMalformedArgumentsAreBothReported()
    {
        var source = new StrictArgumentsSource(["--Sample:Mode=lenient", "stray", "--Sample:Threshold=3"]);

        var reading = source.Read();

        Assert.Equal(["Sample:Mode", "Sample:Threshold"], reading.Entries.Select(entry => entry.Key));
        Assert.Equal("stray", Assert.Single(reading.Problems).Key);
    }
}
