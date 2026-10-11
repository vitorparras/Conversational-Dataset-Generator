using System.Text.Json;
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

    // An absolute path may be declared (another volume is legitimate), but the log names only the
    // file and the key: the declared folders never reach it, present or absent.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AbsolutePathDeclaredInTheConfigurationNeverReachesTheLog(bool present)
    {
        var token = TestContext.Current.CancellationToken;
        var elsewhere = Directory.CreateTempSubdirectory("cdg-elsewhere-");
        try
        {
            var declared = Path.Combine(elsewhere.FullName, "zqx-model.bin");
            if (present)
            {
                await File.WriteAllTextAsync(declared, "x", token);
            }

            using var installation = HostInstallation.Create();
            installation.WriteSettings(JsonSerializer.Serialize(new
            {
                Server = new { Port = "0" },
                Installation = new { BundledFiles = declared },
                Logging = new { FilePath = "logs/core.log" },
            }));
            await using var host = installation.Start();

            if (present)
            {
                Assert.True(await host.WaitForOutputAsync("Núcleo ouvindo", Timeout), host.Output);
            }
            else
            {
                _ = await host.WaitForExitAsync(Timeout);
                AssertNoAbsolutePath(
                    await File.ReadAllTextAsync(Path.Combine(installation.Directory.FullName, "logs", "core.log"), token),
                    installation,
                    elsewhere.FullName);
            }

            Assert.Contains("Conferência Installation:BundledFiles[0]: zqx-model.bin:", host.Output, StringComparison.Ordinal);
            AssertNoAbsolutePath(host.Output, installation, elsewhere.FullName);
        }
        finally
        {
            elsewhere.Delete(recursive: true);
        }
    }

    private static void AssertNoAbsolutePath(string log, HostInstallation installation, params string[] declaredFolders)
    {
        Assert.NotEmpty(log);
        var folders = declaredFolders.Append(installation.Directory.FullName)
            .Select(folder => folder.TrimEnd(Path.DirectorySeparatorChar))
            .ToList();
        Assert.All(log.Split('\n'), line =>
        {
            Assert.All(folders, folder => Assert.DoesNotContain(folder, line, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(@":\", line, StringComparison.Ordinal);
            Assert.DoesNotContain("/home/", line, StringComparison.Ordinal);
        });
    }
}
