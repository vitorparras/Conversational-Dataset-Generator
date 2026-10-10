using System.Security.Cryptography;

namespace BrokenCryptography;

/// <summary>A broken hash algorithm, which the security rules must refuse.</summary>
public static class Digest
{
    /// <summary>Hashes with MD5, if it compiles.</summary>
    public static byte[] Of(byte[] data) => MD5.HashData(data);
}
