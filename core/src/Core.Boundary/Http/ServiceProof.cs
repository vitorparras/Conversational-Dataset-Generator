using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Core.Boundary.Http;

/// <summary>
/// The proof that the process on a port is the language service this core started: the version route, asked without the secret for a new
/// nonce, answers HMAC-SHA256 of the nonce keyed by the secret. Only a holder of the secret can; the proof tells nothing of the secret.
/// </summary>
internal static class ServiceProof
{
    internal const string NonceHeader = "x-service-nonce";
    internal const string ProofHeader = "x-service-proof";

    private const int NonceBytes = 32;
    private const int LongestHead = 8192;
    private const int LongestBody = 4096;
    private const int NameAndValue = 2;
    private const string HeadEnd = "\r\n\r\n";

    /// <summary>Asks for the proof on <paramref name="stream"/> and drains the answer, so the connection is ready for the real request.</summary>
    /// <exception cref="HttpRequestException">The answer is not a complete HTTP answer with the right proof.</exception>
    internal static async Task DemandAsync(Stream stream, int port, string secret, CancellationToken cancellationToken)
    {
        var nonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(NonceBytes));
        var question = string.Create(
            CultureInfo.InvariantCulture,
            $"GET /version HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\n{NonceHeader}: {nonce}\r\n\r\n");
        await stream.WriteAsync(Encoding.ASCII.GetBytes(question), cancellationToken).ConfigureAwait(false);
        var head = await ReadHeadAsync(stream, cancellationToken).ConfigureAwait(false);
        var lines = head.Split("\r\n");
        var proof = HeaderIn(lines, ProofHeader);
        if (!lines[0].StartsWith("HTTP/1.1 200", StringComparison.Ordinal) || proof is null || !Matches(proof, nonce, secret))
        {
            throw Refused();
        }

        if (!int.TryParse(HeaderIn(lines, "content-length"), NumberStyles.None, CultureInfo.InvariantCulture, out var length) || length > LongestBody)
        {
            throw Refused();
        }

        await stream.ReadExactlyAsync(new byte[length], cancellationToken).ConfigureAwait(false);
    }

    private static bool Matches(string proof, string nonce, string secret)
    {
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.ASCII.GetBytes(nonce));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(proof), Encoding.ASCII.GetBytes(Convert.ToHexStringLower(expected)));
    }

    /// <summary>The value of the header, by name without regard to case; null when the answer has none.</summary>
    private static string? HeaderIn(string[] lines, string name) =>
        lines.Skip(1)
            .Select(line => line.Split(':', NameAndValue))
            .Where(parts => parts.Length == NameAndValue && string.Equals(parts[0], name, StringComparison.OrdinalIgnoreCase))
            .Select(parts => parts[1].Trim())
            .FirstOrDefault();

    /// <summary>Reads up to the empty line that ends the head, one byte at a time, so nothing of the body is consumed.</summary>
    private static async Task<string> ReadHeadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var head = new List<byte>();
        var next = new byte[1];
        while (!EndsHead(head))
        {
            if (head.Count >= LongestHead || await stream.ReadAsync(next, cancellationToken).ConfigureAwait(false) == 0)
            {
                throw Refused();
            }

            head.Add(next[0]);
        }

        return Encoding.ASCII.GetString(CollectionsMarshal.AsSpan(head)[..^HeadEnd.Length]);
    }

    private static bool EndsHead(List<byte> head) =>
        head.Count >= HeadEnd.Length && Encoding.ASCII.GetString(CollectionsMarshal.AsSpan(head)[^HeadEnd.Length..]) == HeadEnd;

    private static HttpRequestException Refused() =>
        new(HttpRequestError.ConnectionError, "quem escuta na porta não provou ter o segredo do serviço");
}
