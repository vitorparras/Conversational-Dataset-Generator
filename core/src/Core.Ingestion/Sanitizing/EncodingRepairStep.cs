using System.Text;
using Core.Boundary.Contracts;

namespace Core.Ingestion.Sanitizing;

/// <summary>
/// Repairs the text that was read in the wrong encoding and does nothing else: a sequence of Latin characters that an accent, a typographic mark or an emoji became when its UTF-8
/// bytes were read as Windows-1252 or Latin-1 goes back to the character it was, as many times as it was misread, and every other character stays as it came, since evening out
/// quotes, width, invisible characters, decomposed accents or ellipses is normalization, which loses signal (requirement 005). A sequence with two readings and none dominant,
/// because a byte was lost or because it reads as well as written after a capital A with tilde, is left as it is and counted.
/// </summary>
public sealed class EncodingRepairStep : ISanitizationStep
{
    private const int NotAByte = -1;
    private const int NotOneUnit = -1;
    private const int LostByte = -2;
    private const int ReplacementCharacter = 0xFFFD;
    private const int FirstContinuation = 0x80;
    private const int LastContinuation = 0xBF;
    private const int ContinuationBits = 0x3F;
    private const int BitsPerContinuation = 6;

    /// <summary>The sequences a misreading of Portuguese text leaves, by the character that opens them: Latin-1 symbols and letters, general punctuation and symbols, emoji.</summary>
    private static readonly Sequence[] Sequences =
    [
        new(Lead: 0xC2, Width: 2, Base: 0x80, SecondFrom: 0xA0, SecondTo: 0xBF, TakesWrittenMarks: false),
        new(Lead: 0xC3, Width: 2, Base: 0xC0, SecondFrom: 0x80, SecondTo: 0xBF, TakesWrittenMarks: true),
        new(Lead: 0xE2, Width: 3, Base: 0x2000, SecondFrom: 0x80, SecondTo: 0xBF, TakesWrittenMarks: false),
        new(Lead: 0xF0, Width: 4, Base: 0, SecondFrom: 0x9F, SecondTo: 0x9F, TakesWrittenMarks: false),
    ];

    /// <summary>The bytes Windows-1252 does not define, one of which a lost byte was.</summary>
    private static readonly int[] UndefinedBytes = [0x81, 0x8D, 0x8F, 0x90, 0x9D];

    /// <summary>The characters Windows-1252 writes for the bytes 0x80 to 0x9F, in that order; minus one, which no character is, where the byte is not defined.</summary>
    private static readonly int[] Windows1252 =
    [
        0x20AC, -1, 0x201A, 0x0192, 0x201E, 0x2026, 0x2020, 0x2021, 0x02C6, 0x2030, 0x0160, 0x2039, 0x0152, -1, 0x017D, -1,
        -1, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2013, 0x2014, 0x02DC, 0x2122, 0x0161, 0x203A, 0x0153, -1, 0x017E, 0x0178,
    ];

    private const int NoBreakSpace = 0xA0;

    /// <summary>
    /// The marks a writer puts after a capital A with tilde as often as a misreading does: ellipsis, quotes, dashes, bullet, closing guillemet, middle dot and degree, whose
    /// misread letters Portuguese does not write.
    /// </summary>
    private static readonly int[] WrittenMarks = [0x2026, 0x2018, 0x2019, 0x201C, 0x201D, 0x2013, 0x2014, 0x2022, 0xBB, 0xB7, 0xB0];

    private readonly ITextBoundaries boundaries;

    /// <summary>The repair, which walks the text by the code points the boundary gives.</summary>
    /// <param name="boundaries">Splits the text into its code points and counts what the repair writes.</param>
    public EncodingRepairStep(ITextBoundaries boundaries)
    {
        ArgumentNullException.ThrowIfNull(boundaries);
        this.boundaries = boundaries;
    }

    /// <inheritdoc />
    public string Name => "codificacao";

    /// <inheritdoc />
    public int Version => 1;

    /// <inheritdoc />
    public StepScope? Scope { get; } = new(
        "troca a sequência de caracteres latinos em que um acento, uma pontuação tipográfica ou um emoji se tornou ao ser lido na codificação errada pelo caractere que ela era",
        "todo o resto como veio: aspas, largura, caractere invisível, acento decomposto, reticências, caixa, espaçamento, e a sequência de leitura ambígua");

    /// <inheritdoc />
    public IReadOnlyList<SanitizationEdit> Edits(StepInput input) =>
        [.. Repair(input).Points.Where(point => point.Repaired).Select(point => SanitizationEdit.Rewriting(point.From, point.To, point.Text, boundaries))];

    /// <inheritdoc />
    public int Untreated(StepInput input) => Repair(input).Ambiguous;

    private (List<Point> Points, int Ambiguous) Repair(StepInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var points = boundaries.CodePoints(input.Text).Select((text, at) => new Point(text, text.Length == 1 ? text[0] : NotOneUnit, at, at + 1, false)).ToList();
        while (true)
        {
            var (changed, ambiguous) = Pass(points);
            if (!changed)
            {
                return (points, ambiguous);
            }
        }
    }

    /// <summary>One reading of the text, which repairs every sequence with a single reading and counts those with more.</summary>
    private static (bool Changed, int Ambiguous) Pass(List<Point> points)
    {
        var changed = false;
        var ambiguous = 0;
        for (var at = 0; at < points.Count; at++)
        {
            var (sequence, readings) = ReadingsAt(points, at);
            if (readings.Count == 0)
            {
                continue;
            }

            if (readings.Count == 1 && !ReadsAsWritten(points, at, sequence))
            {
                points[at] = new Point(readings[0].Text, readings[0].Value, points[at].From, points[at + sequence.Width - 1].To, true);
                points.RemoveRange(at + 1, sequence.Width - 1);
                changed = true;
            }
            else
            {
                ambiguous++;
            }
        }

        return (changed, ambiguous);
    }

    /// <summary>
    /// Whether the sequence reads as well as written: a capital A with tilde before a mark a writer puts after it, or ending a word in capitals before a no-break space, which
    /// after anything else is the a with grave accent misread.
    /// </summary>
    private static bool ReadsAsWritten(IList<Point> points, int at, Sequence sequence)
    {
        var next = points[at + 1].Value;
        var endsCapitals = at > 0 && char.IsUpper(points[at - 1].Text, 0);
        return sequence.TakesWrittenMarks && (WrittenMarks.Contains(next) || (endsCapitals && next == NoBreakSpace));
    }

    /// <summary>The sequence that starts here and every character its bytes may have been; none when it is no misread sequence.</summary>
    private static (Sequence Sequence, List<Reading> Readings) ReadingsAt(IList<Point> points, int at)
    {
        var sequence = Array.Find(Sequences, candidate => candidate.Lead == points[at].Value);
        var found = new List<Reading>();
        if (sequence is null || at + sequence.Width > points.Count)
        {
            return (Sequences[0], found);
        }

        var bytes = Enumerable.Range(at, sequence.Width).Select(index => index == at ? sequence.Lead : ByteOf(points[index].Value)).ToArray();
        if (!bytes.Contains(NotAByte))
        {
            Collect(sequence, bytes, found);
        }

        return (sequence, found);
    }

    /// <summary>The characters the bytes may have been, trying every undefined byte where one was lost.</summary>
    private static void Collect(Sequence sequence, int[] bytes, List<Reading> found)
    {
        if (!bytes.Contains(LostByte))
        {
            found.AddRange(Decoded(sequence, bytes) is { } value ? [new Reading(value, Encoding.UTF8.GetString([.. bytes.Select(item => (byte)item)]))] : []);
            return;
        }

        var lost = Array.IndexOf(bytes, LostByte);

        foreach (var undefined in UndefinedBytes)
        {
            var tried = (int[])bytes.Clone();
            tried[lost] = undefined;
            Collect(sequence, tried, found);
        }
    }

    /// <summary>The character of the bytes, when the second one falls where the sequence allows.</summary>
    private static int? Decoded(Sequence sequence, int[] bytes)
    {
        if (bytes[1] < sequence.SecondFrom || bytes[1] > sequence.SecondTo)
        {
            return null;
        }

        var value = sequence.Base;
        for (var index = 1; index < bytes.Length; index++)
        {
            value |= (bytes[index] & ContinuationBits) << (BitsPerContinuation * (bytes.Length - 1 - index));
        }

        return value;
    }

    /// <summary>The continuation byte a character was read from, the lost one for the replacement character, or none.</summary>
    private static int ByteOf(int value)
    {
        if (value is >= FirstContinuation and <= LastContinuation)
        {
            return value;
        }

        if (value == ReplacementCharacter)
        {
            return LostByte;
        }

        var windows = Array.IndexOf(Windows1252, value);
        return windows < 0 ? NotAByte : FirstContinuation + windows;
    }

    /// <summary>
    /// A code point of the text, as the text that writes it and as the value of its one UTF-16 unit, when it has one, with the span of the received text it stands for and
    /// whether it was repaired.
    /// </summary>
    private readonly record struct Point(string Text, int Value, int From, int To, bool Repaired);

    /// <summary>A character the bytes of a sequence may have been: its value and the text that writes it.</summary>
    private readonly record struct Reading(int Value, string Text);

    /// <summary>How a misread sequence is read: the character that opens it, how many it takes, the bits its lead gives and where its second byte may fall.</summary>
    private sealed record Sequence(int Lead, int Width, int Base, int SecondFrom, int SecondTo, bool TakesWrittenMarks);
}
