using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace Core.Persistence.Custody.Verification;

/// <summary>
/// The form of a seal in text: <c>CDG-SELO-1|moment|events|digest|check</c>, in one line. The check is the first four bytes of the SHA-256 of the text before it, in lowercase hexadecimal,
/// so that a digit altered or lost in copying, or a text cut short, is not read as another seal.
/// </summary>
internal static class SealText
{
    private const string Form = "CDG-SELO-1";
    private const char Bar = '|';
    private const string MomentFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const int Fields = 5;
    private const int MomentField = 1;
    private const int CountField = 2;
    private const int DigestField = 3;
    private const int CheckField = 4;
    private const int CheckBytes = 4;
    private const int CheckLength = CheckBytes * 2;
    private const int DigestLength = ChainDigest.Length * 2;

    internal static string Format(ChainSeal seal)
    {
        var moment = XmlConvert.ToString(seal.SealedAt.UtcDateTime, XmlDateTimeSerializationMode.Utc);
        var body = string.Create(CultureInfo.InvariantCulture, $"{Form}{Bar}{moment}{Bar}{seal.Events}{Bar}{seal.Digest}");
        return body + Bar + CheckOf(body);
    }

    internal static SealReading Read(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return SealReading.Refused(SealProblem.Empty);
        }

        var fields = text.Trim().Split(Bar);
        if (fields[0] != Form)
        {
            return SealReading.Refused(SealProblem.UnknownForm);
        }

        return fields.Length != Fields || fields[CheckField].Length != CheckLength ? SealReading.Refused(SealProblem.Incomplete) : ReadFields(fields);
    }

    private static SealReading ReadFields(string[] fields)
    {
        if (!DateTimeOffset.TryParseExact(fields[MomentField], MomentFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var moment))
        {
            return SealReading.Refused(SealProblem.Moment);
        }

        if (!IsCount(fields[CountField], out var events))
        {
            return SealReading.Refused(SealProblem.Count);
        }

        if (!IsDigest(fields[DigestField]))
        {
            return SealReading.Refused(SealProblem.Digest);
        }

        var body = string.Join(Bar, fields.Take(CheckField));
        return string.Equals(fields[CheckField], CheckOf(body), StringComparison.Ordinal)
            ? SealReading.Of(ChainSeal.Of(moment, events, Convert.FromHexString(fields[DigestField])))
            : SealReading.Refused(SealProblem.Check);
    }

    private static bool IsCount(string field, out long events) =>
        long.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out events) && string.Equals(events.ToString(CultureInfo.InvariantCulture), field, StringComparison.Ordinal);

    private static bool IsDigest(string field) => field.Length == DigestLength && field.All(IsLowerHex);

    private static bool IsLowerHex(char character) => character is (>= '0' and <= '9') or (>= 'a' and <= 'f');

    internal static string CheckOf(string body) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body)).AsSpan(0, CheckBytes));
}
