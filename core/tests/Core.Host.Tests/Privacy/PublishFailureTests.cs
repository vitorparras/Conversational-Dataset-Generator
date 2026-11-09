namespace Core.Host.Tests.Privacy;

/// <summary>A publish that fails says why in the first line of the failure and keeps the end of what it wrote.</summary>
public sealed class PublishFailureTests
{
    [Fact]
    public void FirstLineHoldsTheErrorLinesAndTheExitCode()
    {
        var output = "  Core.Platform -> bin/Core.Platform.dll\nsrc/A.cs(3,1): error CS0103: The name 'x' does not exist [Core.csproj]\n  other line\nerror MSB3021: Unable to copy file\n";

        var message = PublishFailure.Describe(1, output);

        var first = message.Split(Environment.NewLine)[0];
        Assert.StartsWith("dotnet publish exited with 1: ", first, StringComparison.Ordinal);
        Assert.Contains("error CS0103", first, StringComparison.Ordinal);
        Assert.Contains("error MSB3021", first, StringComparison.Ordinal);
        Assert.DoesNotContain("other line", first, StringComparison.Ordinal);
    }

    [Fact]
    public void OutputWithoutAnErrorLineGivesItsLastLineAsTheCause()
    {
        var message = PublishFailure.Describe(1, "  first\n  Disk full while writing the output\n");

        Assert.Equal("dotnet publish exited with 1: Disk full while writing the output", message.Split(Environment.NewLine)[0]);
    }

    [Fact]
    public void OnlyTheLastTwoHundredLinesFollowTheFirstLine()
    {
        var output = string.Join('\n', Enumerable.Range(1, 500).Select(number => $"line {number}"));

        var lines = PublishFailure.Describe(1, output).Split(Environment.NewLine);

        Assert.Equal(201, lines.Length);
        Assert.Equal("line 301", lines[1]);
        Assert.Equal("line 500", lines[^1]);
    }

    [Fact]
    public void EmptyOutputStillGivesACause() =>
        Assert.Equal("dotnet publish exited with 7: the publish wrote nothing", PublishFailure.Describe(7, "").Split(Environment.NewLine)[0]);
}
