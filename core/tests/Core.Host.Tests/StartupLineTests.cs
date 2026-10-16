using System.Text.RegularExpressions;

namespace Core.Host.Tests;

/// <summary>The built host writes its own startup lines through the platform's console, one line per event in the fixed format.</summary>
public sealed class StartupLineTests
{
    private static readonly Regex ListeningLine = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z INFO  Microsoft\.Hosting\.Lifetime\[14\] Now listening on: http://127\.0\.0\.1:\d+ address=",
        RegexOptions.Multiline);

    [Fact]
    public async Task ListeningLineComesOutInThePlatformFormat()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        var output = await host.StopAsync();

        Assert.Matches(ListeningLine, output);
    }
}
