using System.Security.Cryptography;
using System.Text;
using Core.Platform.Keys;

namespace Core.Persistence.Recovery;

/// <summary>What says that two copies of the key are the same without showing it: the first eight bytes of an HMAC of a fixed word under the key, in lowercase hexadecimal.</summary>
public static class KeyProof
{
    private const int Bytes = 8;
    private static readonly byte[] Word = Encoding.ASCII.GetBytes("cdg-ensaio-1");

    /// <summary>The proof of the key; the key itself leaves nowhere.</summary>
    /// <param name="key">The master key, which is not released here.</param>
    public static string Of(MasterKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Convert.ToHexStringLower(HMACSHA256.HashData(key.AsSpan(), Word).AsSpan(0, Bytes));
    }
}
