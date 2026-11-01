using System.Security.Cryptography.X509Certificates;
using Core.Platform.Startup;
using Core.Platform.Tls;

namespace Core.Host;

/// <summary>
/// Refuses to start without the material of the encrypted channel, saying what is missing or what was found, never the key; what it
/// read stays with it, for the server to present, until the host ends.
/// </summary>
internal sealed class TlsMaterialCheck(TlsMaterialSource source) : IStartupCheck, IDisposable
{
    private TlsMaterialReading? reading;

    public string Name => "Tls";

    /// <summary>The certificate with its key once the check passed; none before, and none when it failed.</summary>
    internal X509Certificate2? Certificate => reading?.Certificate;

    public async Task<CheckResult> RunAsync(CancellationToken cancellationToken)
    {
        reading?.Dispose();
        reading = await source.ReadAsync(cancellationToken).ConfigureAwait(false);
        const string Origin = "arquivos nomeados por Tls:CertificateFile e Tls:KeyFile";
        return reading.Certificate is null
            ? CheckResult.Failed(Name, $"material do canal cifrado recusado: {string.Join("; ", reading.Problems)}", Origin)
            : CheckResult.Passed(Name, "certificado e chave privada do canal cifrado lidos", Origin);
    }

    public void Dispose() => reading?.Dispose();
}
