using Core.Platform.Versioning;

namespace Core.Tests.Unit.Platform.Versioning;

/// <summary>
/// Issue 51 CA-5 and issue 46 CA-4: how what git answered at build time becomes the state of the
/// tree and the commit. A clean tree reads false, a dirty one true, and git missing or failing
/// reads absent, never a plausible value.
/// </summary>
public sealed class BuildInfoTests
{
    private const string Hash = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void StatusWithNoOutputIsACleanTree() =>
        Assert.Equal(VersionValue.Of("false"), BuildInfo.DirtyTreeFrom("0", "0"));

    [Theory]
    [InlineData("1")]
    [InlineData("57")]
    public void StatusWithAnyOutputIsADirtyTree(string outputLength) =>
        Assert.Equal(VersionValue.Of("true"), BuildInfo.DirtyTreeFrom("0", outputLength));

    [Theory]
    [InlineData("128", "0")]
    [InlineData("128", "41")]
    [InlineData("9009", "0")]
    [InlineData("127", "0")]
    [InlineData("", "0")]
    [InlineData("0", "")]
    public void StatusThatGitDidNotAnswerIsAbsent(string exitCode, string outputLength) =>
        Assert.Same(Absent.Instance, BuildInfo.DirtyTreeFrom(exitCode, outputLength));

    [Fact]
    public void CommitIsTheHashGitPrinted() =>
        Assert.Equal(VersionValue.Of(Hash), BuildInfo.CommitFrom("0", Hash));

    [Theory]
    [InlineData("128", "")]
    [InlineData("9009", "")]
    [InlineData("127", "")]
    [InlineData("", "")]
    [InlineData("0", "")]
    [InlineData("0", "HEAD")]
    [InlineData("128", Hash)]
    public void CommitThatGitDidNotAnswerIsAbsent(string exitCode, string output) =>
        Assert.Same(Absent.Instance, BuildInfo.CommitFrom(exitCode, output));
}
