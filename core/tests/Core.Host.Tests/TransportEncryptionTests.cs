using System.Net;
using System.Net.Sockets;
using System.Text;
using Core.Platform.Startup;
using Core.Tests.Support.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Http;

namespace Core.Host.Tests;

/// <summary>
/// Every exchange with the host is encrypted, also on the loopback: a request in clear gets no content, the traffic on the wire holds no
/// material and no session identifier, and a host without the material of the channel does not start (issue #72, CA-1 to CA-3).
/// </summary>
public sealed class TransportEncryptionTests
{
    private const string Material = "ZQX-MATERIAL-7731-NO-CANAL";
    private const string Session = "ZQX-SESSAO-7731-NO-CANAL";

    [Fact]
    public async Task RequestInClearToTheEncryptedPortGetsNoContent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, host.BaseAddress.Port, cancellationToken);
        var stream = tcp.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes("GET /status HTTP/1.1\r\nHost: x\r\nConnection: close\r\n\r\n"), cancellationToken);

        var answered = await ReadToEndAsync(stream, cancellationToken);

        Assert.DoesNotContain("HTTP/", answered, StringComparison.Ordinal);
        Assert.DoesNotContain("alive", answered, StringComparison.Ordinal);
        Assert.DoesNotContain("ready", answered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StatusOverTheChannelAsksTheBrowserNotToComeBackInClear()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        using var response = await host.GetAsync("/status", cancellationToken);

        Assert.Equal("max-age=31536000", Assert.Single(response.Headers.GetValues("Strict-Transport-Security")));
    }

    [Fact]
    public async Task TrafficSeenOnTheWireHoldsNoMaterialAndNoSessionIdentifier()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);
        using var relay = new RecordingRelay(host.BaseAddress.Port);
        using var client = new HttpClient(SharedTlsMaterial.PinnedHandler()) { BaseAddress = new Uri($"https://127.0.0.1:{relay.Port}") };
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"/analysis?referencia={Material}", UriKind.Relative))
        {
            Content = new StringContent($$"""{"text":"ZQX-FAKE-PERSON {{Material}}","language":"pt","resumes":null}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Cookie", $"sessao={Session}");

        using var response = await client.SendAsync(request, cancellationToken);
        _ = await response.Content.ReadAsStringAsync(cancellationToken);
        var seen = await relay.StopAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); // the identifier is not a session of the host; what is proved here is the wire, not the gate
        Assert.True(seen.Length > 0);
        Assert.Equal(0x16, seen[0]);
        Assert.False(seen.AsSpan().IndexOf(Encoding.ASCII.GetBytes(Material)) >= 0, "The material crossed the wire in clear.");
        Assert.False(seen.AsSpan().IndexOf(Encoding.ASCII.GetBytes(Session)) >= 0, "The session identifier crossed the wire in clear.");
        Assert.False(seen.AsSpan().IndexOf("POST /analysis"u8) >= 0, "The request line crossed the wire in clear.");
    }

    [Fact]
    public async Task HostWithoutTheMaterialIsRefusedNamingWhatIsMissing()
    {
        var installation = HostInstallation.Built(HostProcess.FreeAddress()) with { ProvidesTlsMaterial = false };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Tls:CertificateFile'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("'Tls:KeyFile'", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HostWhoseFilesAreNotThereEndsWithTheCheckCodeAndNamesEachFileAndItsPath()
    {
        using var empty = new Core.Tests.Support.Custody.CustodyFiles();
        var certificate = Path.Combine(empty.Directory, "sem-certificado.pem");
        var key = Path.Combine(empty.Directory, "sem-chave.pem");
        var address = HostProcess.FreeAddress();
        var installation = HostInstallation.Built(address) with
        {
            ProvidesTlsMaterial = false,
            Arguments = [.. HostInstallation.Built(address).Arguments, $"--Tls:CertificateFile={certificate}", $"--Tls:KeyFile={key}"],
        };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.StartupCheckFailed, exit.Code);
        Assert.Contains($"arquivo do certificado (Tls:CertificateFile) não encontrado em '{certificate}'", exit.Error, StringComparison.Ordinal);
        Assert.Contains($"arquivo da chave privada (Tls:KeyFile) não encontrado em '{key}'", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddressInClearIsRefusedNamingTheKeyAndTheValue()
    {
        var installation = HostInstallation.Built(HostProcess.FreeAddress()) with { Arguments = ["--Host:Urls=http://127.0.0.1:5080"] };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Host:Urls'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("http://127.0.0.1:5080", exit.Error, StringComparison.Ordinal);
        Assert.Contains("não serve em claro", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OneAddressInClearAmongEncryptedOnesIsRefusedToo()
    {
        var installation = HostInstallation.Built(HostProcess.FreeAddress()) with { Arguments = ["--Host:Urls=https://127.0.0.1:5080;http://127.0.0.1:5081"] };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Host:Urls'", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void CookiesTheHostIssuesTravelOnlyOnTheEncryptedChannelAndAreOutOfReachOfScripts()
    {
        var options = new CookiePolicyOptions();

        TransportSecurity.CookiePolicy(options);

        Assert.Equal(CookieSecurePolicy.Always, options.Secure);
        Assert.Equal(HttpOnlyPolicy.Always, options.HttpOnly);
    }

    private static async Task<string> ReadToEndAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(TimeSpan.FromSeconds(5));
        var read = new MemoryStream();
        try
        {
            await stream.CopyToAsync(read, limit.Token);
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException)
        {
            // A connection the host reset, or one that stayed silent, is also an answer without content.
        }

        return Encoding.ASCII.GetString(read.ToArray());
    }
}
