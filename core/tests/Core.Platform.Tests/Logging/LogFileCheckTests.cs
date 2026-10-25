using Core.Platform.Logging;
using Core.Platform.Startup;

namespace Core.Platform.Tests.Logging;

/// <summary>An opened log file passes; one that could not be opened fails, naming the key and never a path.</summary>
public sealed class LogFileCheckTests
{
    [Fact]
    public async Task OpenedFilePassesAndNamesTheKey()
    {
        var check = new LogFileCheck("Sample:LogFile", opened: true);

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Passed, result.Status);
        Assert.Equal("Sample:LogFile", result.Name);
    }

    [Fact]
    public async Task FileThatCouldNotBeOpenedFailsNamingTheKeyAsItsSource()
    {
        var check = new LogFileCheck("Sample:LogFile", opened: false);

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("'Sample:LogFile'", result.Message, StringComparison.Ordinal);
        Assert.Equal("Sample:LogFile", result.ExpectedSource);
    }
}
