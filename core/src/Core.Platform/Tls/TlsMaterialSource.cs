using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using Core.Platform.Keys;

namespace Core.Platform.Tls;

/// <summary>
/// The channel's material as the means of delivery: the certificate and the private key, each in a PEM file the configuration points
/// at. The reading refuses, naming every problem it saw, a file that is absent or unreadable, a certificate and a key that are not a
/// pair, a certificate that has expired or is not yet valid, and a private key that is open to anyone besides the process's owner or
/// that lies inside the artifact's folder. What the operator reads names the file and what was found, never the key.
/// </summary>
public sealed class TlsMaterialSource
{
    /// <summary>The configuration key that points at the certificate file.</summary>
    public static string CertificateKey => "Tls:CertificateFile";

    /// <summary>The configuration key that points at the private key file.</summary>
    public static string PrivateKeyKey => "Tls:KeyFile";

    private const int LongestCertificateFile = 65536;
    private const int LongestKeyFile = 16384;

    private const string NotPrivate =
        "o PEM não traz uma chave privada (PRIVATE KEY, RSA PRIVATE KEY ou EC PRIVATE KEY), e uma chave pública ou um certificado não serve";

    private static readonly string[] PrivateLabels = ["PRIVATE KEY", "RSA PRIVATE KEY", "EC PRIVATE KEY"];

    private readonly PemFile certificate;
    private readonly PemFile privateKey;
    private readonly string artifactDirectory;
    private readonly TimeProvider clock;

    /// <summary>The source of the two files, refusing a key that lies inside <paramref name="artifactDirectory"/> and judging validity at the time of <paramref name="clock"/>.</summary>
    public TlsMaterialSource(string certificatePath, string keyPath, string artifactDirectory, TimeProvider clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificatePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactDirectory);
        ArgumentNullException.ThrowIfNull(clock);
        certificate = new PemFile($"arquivo do certificado ({CertificateKey})", Path.GetFullPath(certificatePath), LongestCertificateFile);
        privateKey = new PemFile($"arquivo da chave privada ({PrivateKeyKey})", Path.GetFullPath(keyPath), LongestKeyFile);
        this.artifactDirectory = Path.GetFullPath(artifactDirectory);
        this.clock = clock;
    }

    /// <summary>Reads both files and judges them together; the problems come in one reading, so the operator fixes them in one pass.</summary>
    public async Task<TlsMaterialReading> ReadAsync(CancellationToken cancellationToken)
    {
        var (certificateText, certificateFileProblem) = await TextAsync(certificate, cancellationToken).ConfigureAwait(false);
        var (keyText, keyFileProblem) = await KeyTextAsync(cancellationToken).ConfigureAwait(false);
        var (parsed, certificateProblem) = Certificate(certificateText);
        var (key, keyProblem) = Key(keyText);
        using (parsed)
        using (key)
        {
            string?[] found =
            [
                certificateFileProblem,
                keyFileProblem,
                certificateProblem,
                parsed is null ? null : Validity(parsed),
                keyProblem,
                parsed is not null && key is not null && !Pair(parsed, key)
                    ? $"{certificate.Name} em '{certificate.Path}' e {privateKey.Name} em '{privateKey.Path}': a chave privada não é a do certificado"
                    : null,
            ];
            var problems = found.OfType<string>().ToList();
            return problems.Count > 0 ? TlsMaterialReading.Refused(problems) : TlsMaterialReading.Ready(X509Certificate2.CreateFromPem(parsed!.ExportCertificatePem(), keyText));
        }
    }

    private static string Unopened(PemFile file, Exception exception) =>
        $"{file.Name} em '{file.Path}': não pôde ser aberto ({exception.GetType().Name})";

    /// <summary>What the file holds as text, or why it does not: absent, larger than its kind can be or not opened.</summary>
    private static async Task<(string? Text, string? Problem)> TextAsync(PemFile file, CancellationToken cancellationToken)
    {
        if (!File.Exists(file.Path))
        {
            return (null, $"{file.Name} não encontrado em '{file.Path}'");
        }

        try
        {
            return await ReadWithinLimitAsync(file, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (null, Unopened(file, exception));
        }
    }

    /// <summary>The text, read through the one handle that also tells the size, unless the file is larger than its kind can be.</summary>
    private static async Task<(string? Text, string? Problem)> ReadWithinLimitAsync(PemFile file, CancellationToken cancellationToken)
    {
        var stream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            if (stream.Length > file.Longest)
            {
                return (null, $"{file.Name} em '{file.Path}': maior que {file.Longest} bytes, o que um arquivo desse tipo não é");
            }

            var content = new byte[stream.Length];
            await stream.ReadExactlyAsync(content, cancellationToken).ConfigureAwait(false);
            return (TextOf(content), null);
        }
    }

    /// <summary>The text of the bytes, which are zeroed here; bytes that are not UTF-8 give no text, so the PEM check refuses them.</summary>
    private static string TextOf(byte[] content)
    {
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(content);
        }
        catch (DecoderFallbackException)
        {
            return string.Empty;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(content);
        }
    }

    private static AsymmetricAlgorithm? Imported(string text)
    {
        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(text);
            return rsa;
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException)
        {
            rsa.Dispose();
        }

        var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportFromPem(text);
            return ecdsa;
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException)
        {
            ecdsa.Dispose();
            return null;
        }
    }

    private static string Told(DateTime moment) => XmlConvert.ToString(moment.ToUniversalTime(), XmlDateTimeSerializationMode.Utc);

    /// <summary>Whether the public part of the key is the one the certificate carries.</summary>
    private static bool Pair(X509Certificate2 parsed, AsymmetricAlgorithm key) =>
        parsed.PublicKey.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(key.ExportSubjectPublicKeyInfo());

    /// <summary>The text of the private key file, unless the file may not be read: inside the artifact folder, open to anyone besides the process owner or not opened by the system.</summary>
    private async Task<(string? Text, string? Problem)> KeyTextAsync(CancellationToken cancellationToken) =>
        File.Exists(privateKey.Path) && KeyRefusal() is { } refusal
            ? (null, refusal)
            : await TextAsync(privateKey, cancellationToken).ConfigureAwait(false);

    private string? KeyRefusal()
    {
        try
        {
            var real = RealPath.Of(privateKey.Path);
            if (RealPath.IsWithin(real, artifactDirectory))
            {
                return $"{privateKey.Name} em '{privateKey.Path}', dentro da pasta do artefato; ele fica fora dela";
            }

            return KeyFilePermission.Excess(real) is { } excess ? $"{privateKey.Name} em '{privateKey.Path}': {excess}" : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Unopened(privateKey, exception);
        }
    }

    /// <summary>The certificate the text holds, or why it holds none; no text, no certificate and nothing to tell, for the problem of the file was told.</summary>
    private (X509Certificate2? Certificate, string? Problem) Certificate(string? text)
    {
        if (text is null)
        {
            return (null, null);
        }

        if (!PemEncoding.TryFind(text, out var fields) || !text.AsSpan(fields.Label).SequenceEqual("CERTIFICATE"))
        {
            return (null, $"{certificate.Name} em '{certificate.Path}': não é um certificado em PEM");
        }

        try
        {
            return (X509Certificate2.CreateFromPem(text), null);
        }
        catch (CryptographicException)
        {
            return (null, $"{certificate.Name} em '{certificate.Path}': PEM de certificado ilegível");
        }
    }

    /// <summary>The key as an algorithm that tells its public part, or why the text is not a private key the channel can use; no text, nothing to tell.</summary>
    private (AsymmetricAlgorithm? Key, string? Problem) Key(string? text)
    {
        if (text is null)
        {
            return (null, null);
        }

        if (!PemEncoding.TryFind(text, out var fields))
        {
            return (null, $"{privateKey.Name} em '{privateKey.Path}': não é uma chave privada em PEM");
        }

        if (text.AsSpan(fields.Label).SequenceEqual("ENCRYPTED PRIVATE KEY"))
        {
            return (null, $"{privateKey.Name} em '{privateKey.Path}': chave protegida por senha; a chave fica sem senha, só com permissão do dono");
        }

        if (!Array.Exists(PrivateLabels, label => text.AsSpan(fields.Label).SequenceEqual(label)))
        {
            return (null, $"{privateKey.Name} em '{privateKey.Path}': {NotPrivate}");
        }

        var key = Imported(text);
        return (key, key is null ? $"{privateKey.Name} em '{privateKey.Path}': PEM de chave privada ilegível ou de um tipo que o canal não usa; aceitas RSA e curva elíptica" : null);
    }

    /// <summary>Before the first moment and after the last one the certificate says it is valid; either is told with the date.</summary>
    private string? Validity(X509Certificate2 parsed)
    {
        var now = clock.GetUtcNow();
        if (parsed.NotAfter.ToUniversalTime() < now)
        {
            return $"{certificate.Name} em '{certificate.Path}': certificado vencido em {Told(parsed.NotAfter)}";
        }

        return parsed.NotBefore.ToUniversalTime() > now
            ? $"{certificate.Name} em '{certificate.Path}': certificado ainda não válido, vale a partir de {Told(parsed.NotBefore)}"
            : null;
    }

    private sealed record PemFile(string Name, string Path, int Longest);
}
