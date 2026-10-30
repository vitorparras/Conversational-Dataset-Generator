using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Core.Host.Tests.Analysis;

/// <summary>The answer the language service gives when asked, without the secret, for the proof that it holds it; written here from the contract alone.</summary>
internal static partial class ProofAnswer
{
    /// <summary>The whole HTTP answer to a request that carries a nonce; null when the request carries none.</summary>
    public static string? To(string request, string secret)
    {
        var found = NonceLine().Match(request);
        if (!found.Success)
        {
            return null;
        }

        var nonce = found.Groups[1].Value;
        var proof = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.ASCII.GetBytes(nonce)));
        return $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nx-service-proof: {proof}\r\nContent-Length: 2\r\n\r\n{{}}";
    }

    [GeneratedRegex("^x-service-nonce: ([0-9a-f]+)\r?$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex NonceLine();
}
