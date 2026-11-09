using System.Text;

namespace Core.Host.Submissions;

/// <summary>
/// The stable code of a reason, written from the name of the enumeration that names it: lowercase letters and underscores,
/// the same for the screen, the route and the motive of the batch.
/// </summary>
internal static class ReasonCodes
{
    /// <summary>The code of the file that passed the limit of bytes.</summary>
    internal const string TooLarge = "too_large";

    /// <summary>The code of a file a batch of the domain already accepted, recognized by the digest of what came and never by the name of the file.</summary>
    internal const string AlreadyReceived = "already_received";

    /// <summary>The code of a value of an enumeration, as <c>too_large</c> for <c>TooLarge</c>.</summary>
    internal static string Of<TValue>(TValue value)
        where TValue : struct, Enum
    {
        var code = new StringBuilder();
        foreach (var letter in value.ToString())
        {
            if (!char.IsAsciiLetterUpper(letter))
            {
                _ = code.Append(letter);
                continue;
            }

            if (code.Length > 0)
            {
                _ = code.Append('_');
            }

            _ = code.Append((char)(letter - 'A' + 'a'));
        }

        return code.ToString();
    }
}
