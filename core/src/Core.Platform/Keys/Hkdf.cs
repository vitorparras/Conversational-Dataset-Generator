using System.Security.Cryptography;

namespace Core.Platform.Keys;

/// <summary>HKDF-SHA256 of the standard library with no salt, the extraction the RFC 5869 defines for that case.</summary>
internal static class Hkdf
{
    internal static void Derive(ReadOnlySpan<byte> inputKeyMaterial, ReadOnlySpan<byte> info, Span<byte> output) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, inputKeyMaterial, output, [], info);
}
