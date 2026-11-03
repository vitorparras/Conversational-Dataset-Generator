using System.Collections.ObjectModel;
using Core.Host.Restore;
using Core.Platform.Startup;

namespace Core.Host.Tests.Restore;

/// <summary>The mode that puts a copy back (#64, CA-5): asked for by its word, followed by the copy and the tip noted outside it, and refused with the usual code when it lacks them.</summary>
public sealed class RestoreLoadModeTests
{
    [Fact]
    public void WordAsTheFirstArgumentAsksForTheModeAndTheThreeThatFollowAreNotPassedToTheConfiguration()
    {
        string[] args = ["restore-load", "/zqx/copy", "42", "abc", "--Database:Host=zqx"];

        Assert.True(RestoreLoadMode.Requested(args));
        Assert.Equal(["--Database:Host=zqx"], RestoreLoadMode.ConfigurationArguments(args));
        Assert.False(RestoreLoadMode.Requested(["--Database:Host=zqx", "restore-load"]));
        Assert.Equal(["--Database:Host=zqx"], RestoreLoadMode.ConfigurationArguments(["--Database:Host=zqx"]));
    }

    [Fact]
    public void ModeWithFewerArgumentsThanItNeedsPassesNothingToTheConfiguration()
    {
        Assert.Empty(RestoreLoadMode.ConfigurationArguments(["restore-load", "/zqx/copy"]));
    }

    [Fact]
    public async Task ModeWithoutTheCopyTheTipAndTheMasterKeyIsRefusedWithTheConfigurationCodeAndSaysWhatItNeeds()
    {
        var installation = HostInstallation.Built(HostProcess.FreeAddress());
        installation = installation with { Arguments = ["restore-load", .. installation.Arguments], Environment = Database() };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("restore-load pede a pasta da cópia", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LaunchThatEndedReturnsTheCodeItWasGiven()
    {
        Assert.Equal(ExitCode.RestoreRefused, await HostLaunch.Loaded(ExitCode.RestoreRefused).RunAsync());
        Assert.Equal(ExitCode.Success, await HostLaunch.Loaded(ExitCode.Success).RunAsync());
    }

    private static ReadOnlyDictionary<string, string> Database() =>
        new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CDG_Database__Host"] = "127.0.0.1:1",
            ["CDG_Database__Name"] = "zqx_database",
            ["CDG_Database__Username"] = "zqx_app",
            ["CDG_Database__Password"] = "ZQX-SENHA-7731",
        });
}
