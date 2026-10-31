using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Core.Tests.Support.Custody;

namespace Core.Tests.Support.Net;

/// <summary>A self-signed certificate and its private key, made once for the whole test process, as PEM files open only to the user of the process; nothing here is a real credential.</summary>
public static class SharedTlsMaterial
{
    private static readonly Lazy<(CustodyFiles Files, string Thumbprint)> Material = new(Make);

    /// <summary>The path of the certificate file.</summary>
    public static string CertificatePath => Path.Combine(Material.Value.Files.Directory, "certificate.pem");

    /// <summary>The path of the private key file.</summary>
    public static string KeyPath => Path.Combine(Material.Value.Files.Directory, "key.pem");

    /// <summary>
    /// A client that trusts the server only when it presents this certificate, which no authority signed: the test pins what the host
    /// was given instead of turning the validation off.
    /// </summary>
    public static HttpClientHandler PinnedHandler()
    {
        var thumbprint = Material.Value.Thumbprint;
        return new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) => certificate is not null && certificate.GetCertHashString(HashAlgorithmName.SHA256) == thumbprint,
        };
    }

    private static (CustodyFiles Files, string Thumbprint) Make()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=zqx-teste", key, HashAlgorithmName.SHA256);
        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(30));
        var files = new CustodyFiles();
        _ = files.WriteText("certificate.pem", certificate.ExportCertificatePem());
        _ = files.WriteText("key.pem", key.ExportPkcs8PrivateKeyPem());
        AppDomain.CurrentDomain.ProcessExit += (_, _) => files.Dispose();
        return (files, certificate.GetCertHashString(HashAlgorithmName.SHA256));
    }
}
