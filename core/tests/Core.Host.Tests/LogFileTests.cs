using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>The key that names a log file makes the running host write its console lines there; without it no file appears.</summary>
public sealed class LogFileTests
{
    private const string LogFileName = "ZQX-LOG-7731.log";

    [Fact]
    public async Task LogFileHoldsTheLinesTheConsoleShows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, LogFileName);

        var output = await RunUntilListeningAsync(path, cancellationToken);
        var file = await File.ReadAllTextAsync(path, cancellationToken);

        Assert.Contains(LogLines.Untimed(file), line => line.StartsWith("INFO  Microsoft.Hosting.Lifetime[14] Now listening on: ", StringComparison.Ordinal));
        Assert.All(LogLines.Complete(file), line => Assert.Matches(LogLines.Format(), line));
        Assert.Subset(LogLines.Untimed(file), LogLines.Untimed(output));
    }

    [Fact]
    public async Task LogFilePathAppearsOnNoLine()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, LogFileName);

        var output = await RunUntilListeningAsync(path, cancellationToken);
        var file = await File.ReadAllTextAsync(path, cancellationToken);

        Assert.NotEmpty(file);
        Assert.DoesNotContain("ZQX-LOG-7731", output, StringComparison.Ordinal);
        Assert.DoesNotContain("ZQX-LOG-7731", file, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnopenableLogFileEndsTheProcessWithTheCheckCodeNamingTheKeyNotThePath()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "ZQX-MISSING-7731", LogFileName);
        var installation = WithLogFile(HostInstallation.Built(HostProcess.FreeAddress()), path);

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, cancellationToken);

        Assert.Equal((int)ExitCode.StartupCheckFailed, exit.Code);
        Assert.Contains("'Host:LogFile'", exit.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("ZQX-MISSING-7731", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RelativeLogFileIsCreatedBesideTheBinaryWhateverTheWorkingDirectory()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var copy = new HostCopy(/*lang=json*/ "{}");
        using var elsewhere = new TemporaryDirectory();
        _ = Directory.CreateDirectory(Path.Combine(copy.Path, "logs"));
        _ = Directory.CreateDirectory(Path.Combine(elsewhere.Path, "logs"));
        var address = HostProcess.FreeAddress();
        var arguments = new[] { HostInstallation.UrlsArgument(address), $"--Host:LogFile=logs/{LogFileName}" };
        var installation = copy.Installation(arguments) with { WorkingDirectory = elsewhere.Path };

        using (var host = await HostProcess.StartAsync(installation, address, HostProcess.StartupTimeout, cancellationToken))
        {
            _ = await host.StopAsync();
        }

        Assert.Contains(
            LogLines.Untimed(await File.ReadAllTextAsync(Path.Combine(copy.Path, "logs", LogFileName), cancellationToken)),
            line => line.StartsWith("INFO  Microsoft.Hosting.Lifetime[14] Now listening on: ", StringComparison.Ordinal));
        Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(elsewhere.Path, "logs")));
    }

    [Fact]
    public async Task InvalidLogFilePathEndsTheProcessWithTheCheckCodeNamingTheKeyNotThePath()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var copy = new HostCopy(/*lang=json*/ """{ "Host": { "LogFile": "ZQX-BAD-\u00007731.log" } }""");

        var exit = await HostProcess.RunToExitAsync(copy.Installation(), HostProcess.StartupTimeout, cancellationToken);

        Assert.Equal((int)ExitCode.StartupCheckFailed, exit.Code);
        Assert.Contains("'Host:LogFile'", exit.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("ZQX-BAD", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutTheKeyTheHostCreatesNoFile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var copy = new HostCopy(/*lang=json*/ "{}");
        var before = Directory.GetFileSystemEntries(copy.Path).Order(StringComparer.Ordinal).ToList();
        var address = HostProcess.FreeAddress();

        using (var host = await HostProcess.StartAsync(
            copy.Installation(HostInstallation.UrlsArgument(address)), address, HostProcess.StartupTimeout, cancellationToken))
        {
            _ = await host.StopAsync();
        }

        Assert.Equal(before, Directory.GetFileSystemEntries(copy.Path).Order(StringComparer.Ordinal));
    }

    private static async Task<string> RunUntilListeningAsync(string path, CancellationToken cancellationToken)
    {
        var address = HostProcess.FreeAddress();
        using var host = await HostProcess.StartAsync(
            WithLogFile(HostInstallation.Built(address), path), address, HostProcess.StartupTimeout, cancellationToken);
        return await host.StopAsync();
    }

    private static HostInstallation WithLogFile(HostInstallation installation, string path) =>
        installation with { Arguments = [.. installation.Arguments, $"--Host:LogFile={path}"] };
}
