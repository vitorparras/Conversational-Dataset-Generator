using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Core.Platform.Keys;

/// <summary>
/// How the two custody files are written, and what is said when they are not: the key is one line of base64, the labels one per
/// line in UTF-8; the problems name what was found, never the value.
/// </summary>
internal static class CustodyFileFormat
{
    /// <summary>The most the key file may hold: 44 characters of base64 with room for the line break.</summary>
    internal const int LongestKeyFile = 64;

    /// <summary>The most the labels file may hold, so a file that is not one cannot fill the memory.</summary>
    internal const int LongestLabelsFile = 1_048_576;

    private const byte Space = (byte)' ';

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>The bytes the key file holds, or why it holds none; the content is zeroed here so that no copy of the text stays behind.</summary>
    internal static (byte[]? Key, string? Problem) ParseKey(byte[] content)
    {
        try
        {
            var text = WithoutLastBreak(content);
            if (text.Length == 0)
            {
                return (null, "arquivo vazio");
            }

            var lines = text.Count((byte)'\n') + 1;
            if (lines > 1)
            {
                return (null, $"{lines} linhas, esperada 1");
            }

            return text.IndexOfAnyInRange(byte.MinValue, Space) >= 0 ? (null, "espaço em branco ou caractere de controle na linha") : Decode(text);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(content);
        }
    }

    /// <summary>The labels the labels file holds, or why they cannot be taken from it.</summary>
    internal static (DerivationLabels? Labels, string? Problem) ParseLabels(byte[] content)
    {
        if (content.AsSpan().StartsWith(Encoding.UTF8.Preamble))
        {
            return (null, "começa com a marca de ordem de bytes (BOM); grave UTF-8 sem ela");
        }

        string text;
        try
        {
            text = StrictUtf8.GetString(WithoutLastBreak(content));
        }
        catch (DecoderFallbackException)
        {
            return (null, "não é UTF-8 válido");
        }

        if (text.Length == 0)
        {
            return (DerivationLabels.None, null);
        }

        var lines = text.Split('\n').Select(line => line.EndsWith('\r') ? line[..^1] : line).ToList();
        var flawed = lines.FindIndex(line => line.Length == 0 || line != line.Trim() || line.Any(char.IsControl));
        return flawed < 0 ? (DerivationLabels.Of(lines), null) : (null, $"linha {flawed + 1}: rótulo vazio, com espaço em volta ou com caractere de controle");
    }

    /// <summary>The content without the one line break that may end it.</summary>
    private static ReadOnlySpan<byte> WithoutLastBreak(ReadOnlySpan<byte> content)
    {
        var crLf = "\r\n"u8;
        if (content.EndsWith(crLf))
        {
            return content[..^crLf.Length];
        }

        var lf = "\n"u8;
        return content.EndsWith(lf) ? content[..^lf.Length] : content;
    }

    private static (byte[]? Key, string? Problem) Decode(ReadOnlySpan<byte> text)
    {
        var buffer = new byte[Base64.GetMaxDecodedFromUtf8Length(text.Length)];
        try
        {
            return Base64.DecodeFromUtf8(text, buffer, out _, out var written) == System.Buffers.OperationStatus.Done
                ? (buffer[..written], null)
                : (null, $"não é base64, {text.Length} bytes");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }
}
