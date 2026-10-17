using System.Text.RegularExpressions;

namespace Core.Host.Tests;

/// <summary>The built host takes nothing from the platform's own sources: not the environment variables of the logging or the web host, and nothing leaves a machine path in its log.</summary>
public sealed class PlatformSourcesTests
{
    private static readonly Regex VerboseLine = new(@"^\S+ (TRACE|DEBUG)\s", RegexOptions.Multiline);

    [Fact]
    public async Task LogLevelFromTheEnvironmentDoesNotChangeWhatTheHostLogs()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var address = HostProcess.FreeAddress();
        var installation = HostInstallation.Built(address) with { Environment = Variable("Logging__LogLevel__Default", "Trace") };
        using var host = await HostProcess.StartAsync(installation, address, HostProcess.StartupTimeout, cancellationToken);
        using var response = await host.GetAsync("/status", cancellationToken);

        var output = await host.StopAsync();

        Assert.DoesNotMatch(VerboseLine, output);
    }

    [Fact]
    public async Task UrlsFromThePlatformVariableAreNotListenedOn()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var address = HostProcess.FreeAddress();
        var foreign = HostProcess.FreeAddress();
        var installation = HostInstallation.Built(address) with { Environment = Variable("ASPNETCORE_URLS", foreign.GetLeftPart(UriPartial.Authority)) };
        using var host = await HostProcess.StartAsync(installation, address, HostProcess.StartupTimeout, cancellationToken);
        using var client = new HttpClient { BaseAddress = foreign };

        _ = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync(new Uri("/status", UriKind.Relative), cancellationToken));
    }

    [Fact]
    public async Task StartupLogHoldsNoPathOfTheMachine()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        var output = await host.StopAsync();

        Assert.DoesNotContain(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), output, StringComparison.Ordinal);
        Assert.DoesNotContain("Content root path", output, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> Variable(string name, string value) => new() { [name] = value };
}
