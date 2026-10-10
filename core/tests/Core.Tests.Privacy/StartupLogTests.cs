using Core.Tests.Integration.Host;

namespace Core.Tests.Privacy;

/// <summary>
/// Requirement 021: the startup log carries no absolute path of the installation, which under a
/// user profile carries the account name; neither on the way up nor on a refusal.
/// </summary>
public sealed class StartupLogTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task StartupLogNamesNoAbsolutePath()
    {
        using var installation = HostInstallation.Create();
        await File.WriteAllTextAsync(
            Path.Combine(installation.Directory.FullName, "zqx-model.bin"), "x", TestContext.Current.CancellationToken);
        installation.WriteSettings("""{ "Server": { "Port": "0" }, "Installation": { "BundledFiles": "zqx-model.bin" } }""");
        await using var host = installation.Start();

        Assert.True(await host.WaitForOutputAsync("Núcleo ouvindo", Timeout), host.Output);

        AssertNoAbsolutePath(host.Output, installation);
    }

    [Fact]
    public async Task RefusalLogNamesNoAbsolutePath()
    {
        using var installation = HostInstallation.Create();
        installation.WriteSettings(
            """{ "Server": { "Port": "0" }, "Installation": { "BundledFiles": "models/zqx-model.bin" }, "Logging": { "FilePath": "logs/core.log" } }""");
        await using var host = installation.Start();

        _ = await host.WaitForExitAsync(Timeout);

        Assert.Contains("deveria vir de:", host.Output, StringComparison.Ordinal);
        AssertNoAbsolutePath(host.Output, installation);
        AssertNoAbsolutePath(
            await File.ReadAllTextAsync(
                Path.Combine(installation.Directory.FullName, "logs", "core.log"), TestContext.Current.CancellationToken),
            installation);
    }

    private static void AssertNoAbsolutePath(string log, HostInstallation installation)
    {
        Assert.NotEmpty(log);
        var folder = installation.Directory.FullName.TrimEnd(Path.DirectorySeparatorChar);
        Assert.All(log.Split('\n'), line =>
        {
            Assert.DoesNotContain(folder, line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(@":\", line, StringComparison.Ordinal);
            Assert.DoesNotContain("/home/", line, StringComparison.Ordinal);
        });
    }
}
