using System.Security.Cryptography;

internal static class Sample
{
    internal static void Digest(byte[] data)
    {
        // ruleid: weak-crypto-csharp
        _ = MD5.HashData(data);
        // ruleid: weak-crypto-csharp
        using var sha1 = SHA1.Create();
        // ruleid: weak-crypto-csharp
        using var des = new DESCryptoServiceProvider();
        using var aes = Aes.Create();
        // ruleid: weak-crypto-csharp
        aes.Mode = CipherMode.ECB;
        // ok: weak-crypto-csharp
        _ = SHA256.HashData(data);
        // ok: weak-crypto-csharp
        aes.Mode = CipherMode.CBC;
    }
}
