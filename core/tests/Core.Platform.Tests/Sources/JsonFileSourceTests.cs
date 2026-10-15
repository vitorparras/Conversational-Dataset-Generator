using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;

namespace Core.Platform.Tests.Sources;

/// <summary>The JSON file is flattened into paths; what cannot be read is reported, never thrown.</summary>
public sealed class JsonFileSourceTests
{
    [Fact]
    public void NestedObjectsAreFlattenedIntoSectionAndKeyPathsWithFileOrigin()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("settings.json", /*lang=json*/ """{ "Sample": { "Mode": "lenient", "Inner": { "Depth": "2" } } }""");

        var reading = new JsonFileSource(path).Read();

        Assert.Equal(ConfigurationOrigin.File, reading.Origin);
        Assert.Equal(
            [
                KeyValuePair.Create("Sample:Mode", (string?)"lenient"),
                KeyValuePair.Create("Sample:Inner:Depth", (string?)"2"),
            ],
            reading.Entries);
        Assert.Empty(reading.Problems);
    }

    [Theory]
    [InlineData("3", "3")]
    [InlineData("true", "true")]
    [InlineData("\"text\"", "text")]
    [InlineData("null", null)]
    public void ScalarIsHandedOverAsText(string json, string? expected)
    {
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("settings.json", $$$"""{ "Sample": { "Key": {{{json}}} } }""");

        var reading = new JsonFileSource(path).Read();

        Assert.Equal(KeyValuePair.Create("Sample:Key", expected), Assert.Single(reading.Entries));
    }

    [Fact]
    public void KeyWrittenTwiceInTheFileIsHandedOverTwice()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("settings.json", /*lang=json*/ """{ "Sample": { "Mode": "a", "Mode": "b" } }""");

        var reading = new JsonFileSource(path).Read();

        Assert.Equal(["Sample:Mode", "Sample:Mode"], reading.Entries.Select(entry => entry.Key));
    }

    [Fact]
    public void AbsentFileWritesNothingAndReportsNothing()
    {
        using var directory = new TemporaryDirectory();

        var reading = new JsonFileSource(Path.Combine(directory.Path, "absent.json")).Read();

        Assert.Empty(reading.Entries);
        Assert.Empty(reading.Problems);
    }

    [Fact]
    public void InvalidJsonIsAProblemNamingTheFileNotAnException()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.WriteFile("settings.json", "{ not json");

        var reading = new JsonFileSource(path).Read();

        Assert.Empty(reading.Entries);
        var problem = Assert.Single(reading.Problems);
        Assert.Equal((ConfigurationProblemKind.UnreadableFile, path), (problem.Kind, problem.Key));
        Assert.Contains("ilegível", problem.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyPathIsADefect()
    {
        var defect = Assert.Throws<ArgumentException>(() => new JsonFileSource(" "));

        Assert.Equal("path", defect.ParamName);
    }
}
