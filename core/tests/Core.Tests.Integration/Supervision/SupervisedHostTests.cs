using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Core.Platform.Startup;
using Core.Tests.Integration.Host;

namespace Core.Tests.Integration.Supervision;

/// <summary>
/// Issue 29 and issue 32 CA-4 on the real host: the language service is a startup check, the
/// status is ready only while the service lives, and ending the host ends the service.
/// </summary>
public sealed partial class SupervisedHostTests
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(20);

    [Fact]
    public async Task StatusIsReadyWhileTheServiceLivesAndNotReadyOnceItDiesWhileTheHostStaysAlive()
    {
        using var installation = HostInstallation.Create();
        var port = HostInstallation.FreePort();
        installation.WriteSettings(HostInstallation.SettingsWithPort(port));
        await using var host = installation.Start();
        using var client = new HostClient(host, port);
        var before = await client.GetWhenUpAsync("/status");
        Assert.True(before.GetProperty("ready").GetBoolean());
        Assert.Contains(
            before.GetProperty("checks").EnumerateArray(),
            check => check.GetProperty("name").GetString() == "LanguageService"
                && check.GetProperty("status").GetString() == "passed");

        Kill(ServiceProcessId(host));

        var after = await UntilNotReadyAsync(client);
        Assert.True(after.GetProperty("alive").GetBoolean());
        Assert.False(host.HasExited, "the host must stay alive when the service dies");
        var check = after.GetProperty("checks").EnumerateArray()
            .Single(c => c.GetProperty("name").GetString() == "LanguageService");
        Assert.Equal("failed", check.GetProperty("status").GetString());
        Assert.Contains("encerrou com código", check.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StatusIsReadyWithTheRealService()
    {
        Assert.SkipUnless(LanguageServiceSetup.UvAvailable(), LanguageServiceSetup.UvMissing);
        using var installation = HostInstallation.Create();
        var port = HostInstallation.FreePort();
        installation.WriteSettings(HostInstallation.SettingsWithPort(port));
        await using var host = installation.Start(languageService: LanguageServiceSetup.Real());
        using var client = new HostClient(host, port);

        var status = await client.GetWhenUpAsync("/status");

        Assert.True(status.GetProperty("ready").GetBoolean());
        Kill(ServiceProcessId(host));
        var after = await UntilNotReadyAsync(client);
        Assert.False(after.GetProperty("ready").GetBoolean());
    }

    [Fact]
    public async Task ServiceEndsOnItsOwnWhenTheHostIsKilledWithoutWarning()
    {
        Assert.SkipUnless(LanguageServiceSetup.UvAvailable(), LanguageServiceSetup.UvMissing);
        using var installation = HostInstallation.Create();
        var port = HostInstallation.FreePort();
        installation.WriteSettings(HostInstallation.SettingsWithPort(port));
        await using var host = installation.Start(languageService: LanguageServiceSetup.Real());
        using var client = new HostClient(host, port);
        _ = await client.GetWhenUpAsync("/status");
        var tree = (await ProcessCommandLines.OfTreeAsync(ServiceProcessId(host))).Select(row => row.Id).ToList();
        Assert.All(tree, id => Assert.True(IsRunning(id)));

        // The host alone, not its tree: nothing gets to run its shutdown.
        using (var hostProcess = Process.GetProcessById(host.Id))
        {
            hostProcess.Kill(entireProcessTree: false);
        }

        var waited = Stopwatch.StartNew();
        while (tree.Any(IsRunning))
        {
            Assert.True(waited.Elapsed < Wait, "the service outlived a host killed without warning");
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task RequestedShutdownEndsTheService()
    {
        using var installation = HostInstallation.Create();
        var port = HostInstallation.FreePort();
        installation.WriteSettings(HostInstallation.SettingsWithPort(port));
        await using var host = installation.Start();
        using var client = new HostClient(host, port);
        _ = await client.GetWhenUpAsync("/status");
        var serviceId = ServiceProcessId(host);
        Assert.True(IsRunning(serviceId));

        await ShutdownSignal.SendAsync(host);
        var exitCode = await host.WaitForExitAsync(ExitTimeout);

        Assert.Equal(ExitCodes.Requested, exitCode);
        Assert.False(IsRunning(serviceId), "the service outlived the host");
        Assert.Contains("Serviço de linguagem encerrado pelo núcleo", host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServiceThatDiesOnStartRefusesTheStartupAndNamesTheCause()
    {
        using var installation = HostInstallation.Create();
        installation.WriteSettings("""{ "Server": { "Port": "0" } }""");
        await using var host = installation.Start(languageService: LanguageServiceSetup.Fake("exit"));

        var exitCode = await host.WaitForExitAsync(ExitTimeout);

        Assert.Equal(ExitCodes.StartupCheckFailed, exitCode);
        Assert.Contains("Conferência LanguageService: reprovada", host.Output, StringComparison.Ordinal);
        Assert.Contains("código 7", host.Output, StringComparison.Ordinal);
        Assert.Contains("LanguageService:Command", host.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Núcleo ouvindo", host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServiceThatNeverAnswersRefusesTheStartupAtTheTimeout()
    {
        using var installation = HostInstallation.Create();
        installation.WriteSettings("""{ "Server": { "Port": "0" } }""");
        var setup = LanguageServiceSetup.Fake("hang");
        await using var host = installation.Start(environment: setup.AsEnvironment(timeoutSeconds: 2).ToDictionary(), languageService: setup);

        var exitCode = await host.WaitForExitAsync(ExitTimeout);

        Assert.Equal(ExitCodes.StartupCheckFailed, exitCode);
        Assert.Contains("não respondeu em 2 s", host.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Núcleo ouvindo", host.Output, StringComparison.Ordinal);
        Assert.False(IsRunning(ServiceProcessId(host)), "the service that never answered was left running");
    }

    [Fact]
    public async Task CommandThatDoesNotExistRefusesTheStartupWithoutShowingIt()
    {
        const string Missing = "zqx-no-such-command-7731";
        using var installation = HostInstallation.Create();
        installation.WriteSettings("""{ "Server": { "Port": "0" } }""");
        await using var host = installation.Start(languageService: new LanguageServiceSetup(Missing, string.Empty));

        var exitCode = await host.WaitForExitAsync(ExitTimeout);

        Assert.Equal(ExitCodes.StartupCheckFailed, exitCode);
        Assert.Contains("Conferência LanguageService: reprovada", host.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(Missing, host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HostWithoutTheCommandRefusesTheConfigurationAndNamesTheKey()
    {
        using var installation = HostInstallation.Create();
        installation.WriteSettings("""{ "Server": { "Port": "0" } }""");
        await using var host = HostProcess.Start(installation.AssemblyPath, []);

        var exitCode = await host.WaitForExitAsync(ExitTimeout);

        Assert.Equal(ExitCodes.ConfigurationRefused, exitCode);
        Assert.Contains("'LanguageService:Command'", host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfigurationShowsTheServiceKeysWithTheirOrigin()
    {
        using var installation = HostInstallation.Create();
        var port = HostInstallation.FreePort();
        installation.WriteSettings(HostInstallation.SettingsWithPort(port));
        await using var host = installation.Start();
        using var client = new HostClient(host, port);

        var view = await client.GetWhenUpAsync("/configuration");

        var entries = view.GetProperty("entries").EnumerateArray().ToDictionary(
            e => e.GetProperty("key").GetString()!,
            e => e.GetProperty("origin").GetString());
        Assert.Equal("environment", entries["LanguageService:Command"]);
        Assert.Equal("environment", entries["LanguageService:Arguments"]);
        Assert.Equal("default", entries["LanguageService:StartupTimeoutSeconds"]);
    }

    [GeneratedRegex(@"processo (\d+), porta \d+")]
    private static partial Regex StartedLine();

    private static int ServiceProcessId(HostProcess host)
    {
        var match = StartedLine().Match(host.Output);
        Assert.True(match.Success, "the host did not log the start of the service:\n" + host.Output);
        return int.Parse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture);
    }

    private static void Kill(int id)
    {
        using var process = Process.GetProcessById(id);
        process.Kill(entireProcessTree: true);
    }

    private static bool IsRunning(int id)
    {
        try
        {
            using var process = Process.GetProcessById(id);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static async Task<JsonElement> UntilNotReadyAsync(HostClient client)
    {
        var waited = Stopwatch.StartNew();
        while (waited.Elapsed < Wait)
        {
            var status = await client.GetWhenUpAsync("/status");
            if (!status.GetProperty("ready").GetBoolean())
            {
                return status;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("the status stayed ready after the service died");
    }
}
