using System.Text;

namespace Core.Host.Tests.Privacy;

/// <summary>Every form in which the bytes of a key can be written down, and the search for them in files.</summary>
internal static class KeySpellings
{
    /// <summary>The bytes as they are, the base64 text, and the hexadecimal text in both cases, each as the bytes a file would hold.</summary>
    public static IReadOnlyList<byte[]> Of(byte[] key) =>
        [
            key,
            Encoding.ASCII.GetBytes(Convert.ToBase64String(key)),
            Encoding.ASCII.GetBytes(Convert.ToHexString(key)),
            Encoding.ASCII.GetBytes(Convert.ToHexStringLower(key)),
        ];

    /// <summary>Whether the content holds the bytes in any of their forms.</summary>
    public static bool HoldsAny(ReadOnlySpan<byte> content, byte[] key)
    {
        foreach (var spelling in Of(key))
        {
            if (content.IndexOf(spelling) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The files, among those given, that hold the bytes in any of their forms; a file that is gone by the time it is read holds nothing.</summary>
    public static IReadOnlyList<string> FilesHolding(IEnumerable<string> files, byte[] key) =>
        [.. files.Where(file => HoldsAny(ContentOf(file), key))];

    private static byte[] ContentOf(string file)
    {
        try
        {
            return File.ReadAllBytes(file);
        }
        catch (FileNotFoundException)
        {
            return [];
        }
    }
}
