using System.Text;
using System.Text.RegularExpressions;

namespace Core.Persistence.Tests.People;

/// <summary>
/// What a record must not hold of a secret, in any of the ways a record writes bytes: as text, as hex, as hex with dashes,
/// and as base64 with or without padding and in its URL form.
/// </summary>
internal static partial class SecretEncodings
{
    /// <summary>The ways the password is written.</summary>
    internal static IEnumerable<string> Of(string password) => [password, .. Of(Encoding.UTF8.GetBytes(password))];

    /// <summary>The ways the bytes are written.</summary>
    internal static IEnumerable<string> Of(byte[] bytes)
    {
        var base64 = Convert.ToBase64String(bytes);
        return
        [
            Convert.ToHexString(bytes),
            Convert.ToHexStringLower(bytes),
            BitConverter.ToString(bytes),
            base64,
            base64.TrimEnd('='),
            base64.TrimEnd('=').Replace('+', '-').Replace('/', '_'),
        ];
    }

    /// <summary>Fails when the text holds any of the secrets, in any of the encodings.</summary>
    internal static void AssertNoneIn(string text, IEnumerable<string> secrets) =>
        Assert.All(secrets, secret => Assert.DoesNotContain(secret, text, StringComparison.OrdinalIgnoreCase));

    /// <summary>Fails when the text holds bytes in the form the server prints them, which is how a refusal of the server carries the row that failed.</summary>
    internal static void AssertNoByteaIn(string text) => Assert.DoesNotMatch(ByteaLiteral(), text);

    [GeneratedRegex(@"\\x[0-9a-fA-F]{16}")]
    private static partial Regex ByteaLiteral();
}
