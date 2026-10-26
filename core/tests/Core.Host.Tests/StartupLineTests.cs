using System.Text.RegularExpressions;

namespace Core.Host.Tests;

/// <summary>The built host writes its own startup lines through the platform's console, one line per event in the fixed format.</summary>
public sealed class StartupLineTests
{
    private static readonly Regex ListeningLine = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z INFO  Microsoft\.Hosting\.Lifetime\[14\] Now listening on: http://127\.0\.0\.1:\d+ address=",
        RegexOptions.Multiline);

    private static readonly Regex VersionsLine = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z INFO  Core\.Host\[200\] Versões em execução .*$", RegexOptions.Multiline);

    [Fact]
    public async Task ListeningLineComesOutInThePlatformFormat()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        var output = await host.StopAsync();

        Assert.Matches(ListeningLine, output);
    }

    [Fact]
    public async Task VersionsLineNamesTheVersionAndCommitAndMarksWhatIsAbsent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        var line = VersionsLine.Match(await host.StopAsync()).Value;

        Assert.Contains(" CoreVersion=0.1.0", line, StringComparison.Ordinal);
        Assert.Contains($" Commit={WorkingCopy.Head()}", line, StringComparison.Ordinal);
        Assert.Contains($" WorkingTree={WorkingCopy.TreeState()}", line, StringComparison.Ordinal);
        Assert.Contains(" ModelVersion=<absent>", line, StringComparison.Ordinal);
        Assert.Contains(" SchemaVersion=<absent>", line, StringComparison.Ordinal);
    }
}
