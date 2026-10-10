namespace Core.Tests.Integration.Host;

/// <summary>Issue 24 CA-1: the real process starts and answers its state.</summary>
public sealed class StatusTests
{
    [Fact]
    public async Task RealProcessStartsAndAnswersStatus()
    {
        using var installation = HostInstallation.Create();
        var port = HostInstallation.FreePort();
        installation.WriteSettings(HostInstallation.SettingsWithPort(port));
        await using var host = installation.Start();
        using var client = new HostClient(host, port);

        var status = await client.GetWhenUpAsync("/status");

        Assert.True(status.GetProperty("alive").GetBoolean());
    }
}
