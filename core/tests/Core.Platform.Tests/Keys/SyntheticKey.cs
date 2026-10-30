using Core.Platform.Keys;

namespace Core.Platform.Tests.Keys;

/// <summary>Keys that belong to no installation, with bytes nobody would pick by chance, and the ways the value could be written out.</summary>
internal static class SyntheticKey
{
    /// <summary>A key of the size every master key has.</summary>
    public static byte[] OfLength(int length) => [.. Enumerable.Range(0, length).Select(index => (byte)(0xA5 ^ ((index * 37) + 11)))];

    public static byte[] Valid() => OfLength(MasterKey.Length);

    /// <summary>Every text a log or an exception could write the value as: hexadecimal in both cases and base64.</summary>
    public static string[] Spellings(byte[] key) => [Convert.ToHexString(key), Convert.ToHexStringLower(key), Convert.ToBase64String(key)];
}
