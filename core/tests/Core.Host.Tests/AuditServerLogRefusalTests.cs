using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>
/// The serving host does not start without the folder of the database server's log, refuses one that is not an absolute path, and ends
/// with the check code when the folder is not one it can read the log from (issue #60, part 2).
/// </summary>
public sealed class AuditServerLogRefusalTests
{
    [Fact]
    public async Task HostWithoutTheFolderEndsWithTheRefusalCodeAndNamesTheKey()
    {
        var exit = await RunWithAsync();

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Audit:ServerLogDirectory'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("não tem valor padrão", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FolderThatIsNotAnAbsolutePathIsRefusedNamingTheKeyAndTheValue()
    {
        var exit = await RunWithAsync("--Audit:ServerLogDirectory=var/log/postgresql");

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Audit:ServerLogDirectory'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("'var/log/postgresql'", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FolderWithNoLogFileEndsWithTheCheckCodeAndNamesTheKey()
    {
        using var empty = new Core.Tests.Support.Custody.CustodyFiles();

        var exit = await RunWithAsync($"--Audit:ServerLogDirectory={empty.Directory}");

        Assert.Equal((int)ExitCode.StartupCheckFailed, exit.Code);
        Assert.Contains("Audit:ServerLogDirectory", exit.Error, StringComparison.Ordinal);
        Assert.Contains(".csv", exit.Error, StringComparison.Ordinal);
    }

    private static Task<HostExit> RunWithAsync(params string[] arguments)
    {
        var installation = HostInstallation.Built(HostProcess.FreeAddress()) with { ProvidesServerLog = false };
        installation = installation with { Arguments = [.. installation.Arguments, .. arguments] };
        return HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);
    }
}
