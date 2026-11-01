using System.Net;
using System.Net.Sockets;
using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>A failure that ends the process after it had begun to serve is neither success nor an unhandled exception: it has a code of its own and names the kind of failure (#381).</summary>
public sealed class ServingFailureTests
{
    private const string SecretInMessage = "ZQX-MATERIAL-7731";

    [Fact]
    public async Task PortAlreadyTakenEndsTheProcessWithTheServingFailedCodeNamingTheKindOfFailure()
    {
        using var taken = new TcpListener(IPAddress.Loopback, 0);
        taken.Start();
        var address = new Uri($"https://127.0.0.1:{((IPEndPoint)taken.LocalEndpoint).Port}");

        var exit = await HostProcess.RunToExitAsync(HostInstallation.Built(address), HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.ServingFailed, exit.Code);
        Assert.Contains("O processo parou: uma falha o encerrou ao abrir a porta ou ao servir", exit.Error, StringComparison.Ordinal);
        Assert.Contains("(System.IO.IOException)", exit.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Unhandled exception", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ServingFailedIsTheInternalSoftwareErrorOfTheSysexitsConventionAndSharesItsNumberWithNoOtherCode()
    {
        var codes = Enum.GetValues<ExitCode>().Select(code => (int)code).ToList();

        Assert.Equal(70, (int)ExitCode.ServingFailed);
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public void ReportNamesTheKindOfFailureAndNeverItsMessage()
    {
        using var output = new StringWriter();

        var code = ServingFailure.Report(output, new InvalidOperationException(SecretInMessage));

        Assert.Equal(ExitCode.ServingFailed, code);
        Assert.Contains("(System.InvalidOperationException)", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(SecretInMessage, output.ToString(), StringComparison.Ordinal);
    }
}
