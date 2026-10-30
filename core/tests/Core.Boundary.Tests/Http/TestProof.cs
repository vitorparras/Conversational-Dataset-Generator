using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Core.Boundary.Tests.Http;

/// <summary>The proof of the language service, written here a second time and from the contract alone: HMAC-SHA256 of the nonce text keyed by the secret text, in lowercase hexadecimal.</summary>
internal static partial class TestProof
{
    public const string NonceHeader = "x-service-nonce";
    public const string ProofHeader = "x-service-proof";

    public static string Of(string secret, string nonce)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexStringLower(hmac.ComputeHash(Encoding.ASCII.GetBytes(nonce)));
    }

    /// <summary>The proof the mode gives for the nonce; null when the mode gives none.</summary>
    public static string? Given(ProofMode mode, string nonce) => mode switch
    {
        ProofMode.Right => Of(Reply.Secret, nonce),
        ProofMode.Wrong => Of(Reply.Secret, new string('0', nonce.Length)),
        ProofMode.Missing => null,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a proof mode."),
    };

    /// <summary>The nonce a request written out as text carries; null when it carries none.</summary>
    public static string? NonceIn(string request)
    {
        var found = NonceLine().Match(request);
        return found.Success ? found.Groups[1].Value : null;
    }

    /// <summary>The whole HTTP answer the version route gives to a nonce, as bytes on a socket.</summary>
    public static byte[] Answer(ProofMode mode, string nonce)
    {
        var proof = Given(mode, nonce) is { } given ? $"{ProofHeader}: {given}\r\n" : string.Empty;
        return Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n{proof}Content-Length: 2\r\n\r\n{{}}");
    }

    [GeneratedRegex("^x-service-nonce: ([0-9a-f]+)\r?$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex NonceLine();
}
