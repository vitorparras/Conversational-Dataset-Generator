using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Core.Platform.Tests.Tls;

/// <summary>A self-signed certificate made for one test, with the private key of it, both as PEM; nothing here is a real credential.</summary>
internal sealed record SyntheticCertificate(string CertificatePem, string KeyPem)
{
    /// <summary>The moment every test of the channel's material takes as now.</summary>
    internal static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Valid from a day before <see cref="Now"/> to a day after it.</summary>
    internal static SyntheticCertificate Valid(bool rsa = false) => Create(Now.AddDays(-1), Now.AddDays(1), rsa);

    internal static SyntheticCertificate Create(DateTimeOffset notBefore, DateTimeOffset notAfter, bool rsa = false)
    {
        if (rsa)
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=zqx-teste", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Made(request.CreateSelfSigned(notBefore, notAfter), key.ExportPkcs8PrivateKeyPem());
        }

        using var curve = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return Made(new CertificateRequest("CN=zqx-teste", curve, HashAlgorithmName.SHA256).CreateSelfSigned(notBefore, notAfter), curve.ExportPkcs8PrivateKeyPem());
    }

    private static SyntheticCertificate Made(X509Certificate2 certificate, string keyPem)
    {
        using (certificate)
        {
            return new SyntheticCertificate(certificate.ExportCertificatePem(), keyPem);
        }
    }
}
