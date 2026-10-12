namespace Core.Tests.Architecture.CodeRules;

/// <summary>Production files and methods stay within the size limits, with one fixture that breaks each limit.</summary>
public sealed class FileAndMethodLimitsTests
{
    [Fact]
    public void ProductionFilesRespectTheFileLimits()
    {
        var violations = SourceTree.ProductionFiles().SelectMany(FileLimits.Check);

        Assert.Empty(violations);
    }

    [Fact]
    public void ProductionFilesRespectTheMethodLimits()
    {
        var violations = SourceTree.ProductionFiles().SelectMany(MethodLimits.Check);

        Assert.Empty(violations);
    }

    [Fact]
    public void FileOverTwoHundredAndFiftyLinesIsRefused()
    {
        var fixture = SourceTree.Fixture("FileOver250Lines.cs");

        var violation = Assert.Single(FileLimits.CheckLength(fixture));

        Assert.Contains("251 lines", violation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SecondPublicTypeInAFileIsRefused()
    {
        var fixture = SourceTree.Fixture("TwoPublicTypes.cs");

        var violation = Assert.Single(FileLimits.CheckPublicTypes(fixture));

        Assert.Equal(8, violation.Line);
    }

    [Fact]
    public void TupleInAPublicSignatureIsRefused()
    {
        var fixture = SourceTree.Fixture("TupleInPublicSignature.cs");

        var violation = Assert.Single(FileLimits.CheckTuplesInPublicSignatures(fixture));

        Assert.Equal(5, violation.Line);
    }

    [Fact]
    public void MethodOverFortyLinesIsRefused()
    {
        var fixture = SourceTree.Fixture("MethodOver40Lines.cs");

        var violation = Assert.Single(MethodLimits.CheckLength(fixture));

        Assert.Contains("41 lines", violation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FifthParameterIsRefused()
    {
        var fixture = SourceTree.Fixture("FiveParameters.cs");

        var violation = Assert.Single(MethodLimits.CheckParameters(fixture));

        Assert.Contains("5 parameters", violation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ThirdNestingLevelIsRefused()
    {
        var fixture = SourceTree.Fixture("NestingDepthThree.cs");

        var violation = Assert.Single(MethodLimits.CheckNesting(fixture));

        Assert.Equal(13, violation.Line);
    }

    [Fact]
    public void ElseIfChainDoesNotCountAsNesting()
    {
        var fixture = SourceTree.Fixture("ElseIfChain.cs");

        Assert.Empty(MethodLimits.CheckNesting(fixture));
    }
}
