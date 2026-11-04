using System.Globalization;
using System.Xml;
using Core.Persistence.Custody.Verification;

namespace Core.Persistence.Recovery;

/// <summary>
/// The receipt of a rehearsal of the recovery: when it was made, the top of the copy (its sequence and digest), the proof of the key it opened with and how many samples opened and failed,
/// and nothing of the content. Its text is <c>CDG-ENSAIO-1|moment|sequence|digest|key|raw|maps|failed|check</c>, with a check code like the seal's, so that a digit altered is refused.
/// </summary>
/// <param name="At">The moment of the rehearsal, in UTC and in whole seconds.</param>
/// <param name="TopSequence">The sequence of the last event of the copy.</param>
/// <param name="TopDigest">The digest of that event, in 64 lowercase hexadecimal digits.</param>
/// <param name="KeyProof">The proof of the key, as <see cref="KeyProof.Of" /> gives it.</param>
/// <param name="Counts">What opened and what did not.</param>
public sealed record RecoveryReceipt(DateTimeOffset At, long TopSequence, string TopDigest, string KeyProof, SampleCounts Counts)
{
    private const string Form = "CDG-ENSAIO-1";
    private const char Bar = '|';
    private const int Fields = 9;
    private const string MomentFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const int MomentField = 1;
    private const int SequenceField = 2;
    private const int DigestField = 3;
    private const int ProofField = 4;
    private const int RawField = 5;
    private const int MapsField = 6;
    private const int FailedField = 7;
    private const int DigestLength = 64;
    private const int ProofLength = 16;

    /// <summary>The form in text: one line, apart by a vertical bar, the check code last.</summary>
    public string ToText()
    {
        var moment = XmlConvert.ToString(At.UtcDateTime, XmlDateTimeSerializationMode.Utc);
        var body = string.Create(CultureInfo.InvariantCulture, $"{Form}{Bar}{moment}{Bar}{TopSequence}{Bar}{TopDigest}{Bar}{KeyProof}{Bar}{Counts.Raw}{Bar}{Counts.Maps}{Bar}{Counts.Failed}");
        return body + Bar + SealText.CheckOf(body);
    }

    /// <summary>Reads a receipt back from its text; one that was altered, cut short or is of another form is not read, and the answer says why.</summary>
    /// <param name="text">The text <see cref="ToText" /> gave; blanks around it are ignored.</param>
    public static RecoveryReading Read(string? text)
    {
        var fields = (text ?? string.Empty).Trim().Split(Bar);
        if (fields[0] != Form)
        {
            return RecoveryReading.Refused(text is null or "" ? "não há recibo" : "o texto não é um recibo desta versão do ensaio");
        }

        if (fields.Length != Fields)
        {
            return RecoveryReading.Refused("o recibo está incompleto");
        }

        if (fields[^1] != SealText.CheckOf(string.Join(Bar, fields[..^1])))
        {
            return RecoveryReading.Refused("o recibo foi alterado ou copiado errado: o código de conferência não fecha");
        }

        return Parse(fields) is { } receipt ? RecoveryReading.Of(receipt) : RecoveryReading.Refused("um campo do recibo não é válido");
    }

    private static RecoveryReceipt? Parse(string[] fields)
    {
        var style = CultureInfo.InvariantCulture;
        if (!DateTimeOffset.TryParseExact(fields[MomentField], MomentFormat, style, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at)
            || !long.TryParse(fields[SequenceField], NumberStyles.None, style, out var sequence))
        {
            return null;
        }

        if (!IsLowerHex(fields[DigestField], DigestLength) || !IsLowerHex(fields[ProofField], ProofLength))
        {
            return null;
        }

        return TryCount(fields[RawField], out var raw) && TryCount(fields[MapsField], out var maps) && TryCount(fields[FailedField], out var failed)
            ? new RecoveryReceipt(at, sequence, fields[DigestField], fields[ProofField], new SampleCounts(raw, maps, failed))
            : null;
    }

    private static bool TryCount(string field, out int count) => int.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out count);

    private static bool IsLowerHex(string field, int length) => field.Length == length && field.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
