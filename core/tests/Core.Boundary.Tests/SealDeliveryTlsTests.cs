using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Core.Boundary.Http;

namespace Core.Boundary.Tests;

/// <summary>
/// The sending of the seal over a real handshake (issue #637, M2): a server on the loopback, on an ephemeral port, answers with a certificate it signed itself, and the handler the host sends with,
/// whose validation is the platform's and has no way round it, rejects it; the failure says the certificate was rejected and the server never gets the seal. The time that runs out is not tried
/// over a socket: the limit of the request is the 30 seconds that <see cref="SealDelivery" /> fixes, and a hook to shorten it would be a way into the production code only for a test.
/// </summary>
public sealed class SealDeliveryTlsTests
{
    private const string Text = "CDG-SELO-1|2031-03-04T05:06:07Z|5|0000000000000000000000000000000000000000000000000000000000000000|0000";
    private const int KeyBits = 2048;
    private const int TestDeadlineMilliseconds = 60_000;

    [Fact(Timeout = TestDeadlineMilliseconds)]
    public async Task ServerWithACertificateItSignedItselfIsRejectedByTheHandlerTheHostSendsWithAndNeverGetsTheSeal()
    {
        var token = TestContext.Current.CancellationToken;
        using var certificate = SelfSigned();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var served = HandshakeAsync(listener, certificate, token);
        using var delivery = new SealDelivery(new Uri($"https://127.0.0.1:{port}/envio"), SealDelivery.Handler());

        var result = await delivery.SendAsync(Text, token);
        var received = await served;

        Assert.Equal((false, SealDeliveryFailure.CertificateRejected, 0), (result.Delivered, result.Failure, received));
    }

    private static X509Certificate2 SelfSigned()
    {
        using var key = RSA.Create(KeyBits);
        var request = new CertificateRequest("CN=selo.zqx", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        using var made = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(made.Export(X509ContentType.Pfx), null);
    }

    /// <summary>Answers the handshake of the one client that comes, and returns how many bytes of the request it read after it: none when the client turned back at the certificate.</summary>
    private static async Task<int> HandshakeAsync(TcpListener listener, X509Certificate2 certificate, CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        var stream = new SslStream(client.GetStream());
        await using (stream.ConfigureAwait(false))
        {
            try
            {
                await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate }, cancellationToken);
                return await stream.ReadAsync(new byte[1], cancellationToken);
            }
            catch (Exception turnedBack) when (turnedBack is IOException or AuthenticationException)
            {
                return 0;
            }
        }
    }
}
