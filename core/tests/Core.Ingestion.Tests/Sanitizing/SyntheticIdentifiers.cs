using CsCheck;

namespace Core.Ingestion.Tests.Sanitizing;

/// <summary>
/// Identifiers in closed form, synthetic by construction: the check digits of a document are always wrong, a telephone has the area code 00, which does not exist, a device has the
/// documentation range of the physical addresses or the reporting body 00 of the test devices, an electronic address ends in the reserved domain .invalid, and a plate has the
/// letters QZX. Each one is written in an arbitrary variation of case, separator, spacing and digit script, of category Nd or No, split or not by invisible characters, and carries
/// its skeleton: its letters and digits, in ASCII and in capitals, which no recognizable variation of it leaves out. A mark of a list or a long spacing joins only the groups of a
/// layout a list does not take: the card number, whose four groups of four a list of years also takes, is split only by the usual separators.
/// </summary>
internal static class SyntheticIdentifiers
{
    /// <summary>The characters that show nothing: of format, marks of no width, variation selectors and the fillers of Hangul.</summary>
    internal static readonly string[] Invisibles =
        ["\u200B", "\u200C", "\u200D", "\u2060", "\uFEFF", "\u00AD", "\uFE0F", "\u034F", "\u180B", "\U000E0100", "\u3164", "\u115F"];

    private static readonly string[] Spread =
    [
        "", " ", "  ", "   ", "    ", " \t", "\u00A0", "\u2009", "\u2003\u2003\u2003\u2003", "/", "\u2024", "\u00B7", "\u2027", "\u2011", "\u2013", "\u2014", "\uFF0E",
        "\u2212", ",", ";", "\u2800", "\u2219", "\u22C5", "\u2215", "\u2044",
    ];

    /// <summary>The separators of the spread that stand in the place of a dot or a space, without the mark of a list and the long spacing.</summary>
    private static readonly string[] Usual = [.. Spread.Except([";", "    ", "\u2003\u2003\u2003\u2003"])];

    private static readonly string[] Dashes = ["-", "\u2010", "\u2011", "\u2013", "\u2212"];

    private static readonly string[] PlateSeparators = ["", "-", "\u2011", "."];

    private static readonly string[] DeviceSeparators = [":", "-"];

    /// <summary>Every closed form the removal answers for, in its usual writing.</summary>
    public static Gen<Form> Forms { get; } = Gen.OneOf(
        Digits(9).Select(body => Digit(string.Empty, [body[..3], body[3..6], body[6..], Wrong(body, [10, 9, 8, 7, 6, 5, 4, 3, 2], [11, 10, 9, 8, 7, 6, 5, 4, 3, 2])], [".", ".", "-"])),
        Digits(12).Select(body => Digit(
            string.Empty,
            [body[..2], body[2..5], body[5..8], body[8..], Wrong(body, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2], [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2])],
            [".", ".", "/", "-"])),
        Gen.Select(Digits(4), Digits(4), Gen.Bool, (first, last, country) => country
            ? Digit("+", ["55", "00", $"9{first}", last], [" (", ") ", "-"])
            : Digit("(", ["00", $"9{first}", last], [") ", "-"])),
        Digits(13).Select(rest => Digit(string.Empty, ["00", rest[..6], rest[6..12], rest[12..]], ["-", "-", "-"])),
        Digits(12).Select(rest => new Form(string.Empty, ["0000", rest[..4], rest[4..8], rest[8..]], [" ", " ", " "], Usual)),
        Gen.Select(Digits(5), Digits(1), (body, check) => new Form(string.Empty, [body, check], ["-"], Dashes)),
        Gen.Select(Digits(3), Gen.Bool, Gen.Char['A', 'Z'], (digits, mercosul, letter) =>
            new Form(string.Empty, ["QZX", $"{digits[0]}{(mercosul ? letter : digits[1])}{digits[1..]}"], [string.Empty], PlateSeparators)),
        Gen.Int[0, 255].Select(last => new Form(string.Empty, ["00", "00", "5E", "00", "53", $"{last:X2}"], [":", ":", ":", ":", ":"], DeviceSeparators)),
        Gen.Select(Gen.Char['a', 'z'].Array[3], Gen.OneOf("", "\u00E3", "\u00E9", "\u00E7"), (letters, accent) =>
            new Form(string.Empty, [$"zqx{accent}{new string(letters)}", "exemplo", "invalid"], ["@", "."], [])));

    /// <summary>An identifier written in an arbitrary variation, split by at least one invisible character when asked.</summary>
    public static Gen<Planted> Variants(bool split) =>
        Gen.Select(Forms, Gen.Int[0, int.MaxValue].Array[8], Gen.Int[split ? 1 : 0, 3], Gen.Int[0, 5], (form, picks, invisibles, writing) => form.Written(picks, invisibles, writing));

    private static Gen<string> Digits(int count) => Gen.Char['0', '9'].Array[count].Select(digits => new string(digits));

    private static Form Digit(string opening, string[] groups, string[] separators) => new(opening, groups, separators, Spread);

    /// <summary>The two check digits of the body by the weights given, the first one moved by one, so that no real number comes out.</summary>
    private static string Wrong(string body, int[] first, int[] second)
    {
        var right = Check(body, first);
        return $"{(right + 1) % 10}{Check($"{body}{right}", second)}";
    }

    private static int Check(string digits, int[] weights)
    {
        var rest = digits.Zip(weights, (digit, weight) => (digit - '0') * weight).Sum() % 11;
        return rest < 2 ? 0 : 11 - rest;
    }

    /// <summary>A closed form: what opens it, its groups and the separators of its usual writing, and the separators a variation may put in their place.</summary>
    internal sealed record Form(string Opening, string[] Groups, string[] Separators, string[] Alternatives)
    {
        public Planted Written(int[] picks, int invisibles, int writing)
        {
            var separators = Separators.Select((usual, at) => Alternatives.Length == 0 || picks[at] % 2 == 0 ? usual : Alternatives[picks[at] / 2 % Alternatives.Length]);
            var text = Opening + string.Concat(Groups.Zip(separators.Append(string.Empty), (group, separator) => group + separator));
            text = writing switch
            {
                1 => text.ToUpperInvariant(),
                2 => string.Concat(text.Select(char.ToLowerInvariant)),
                > 2 => new string([.. text.Select(character => char.IsAsciiDigit(character) && (Alternatives == Spread || Alternatives == Usual) ? InScript(character - '0', writing) : character)]),
                _ => text,
            };
            for (var time = 0; time < invisibles; time++)
            {
                var at = 1 + (picks[^(time + 1)] % (text.Length - 1));
                at += char.IsLowSurrogate(text[at]) ? 1 : 0;
                text = text.Insert(at, Invisibles[picks[time] % Invisibles.Length]);
            }

            return new Planted(text, string.Concat(Groups).ToUpperInvariant());
        }

        /// <summary>The digit in the script of the writing: fullwidth, of category Nd; circled or superscript, of category No.</summary>
        private static char InScript(int digit, int writing) => (writing, digit) switch
        {
            (3, _) => (char)('\uFF10' + digit),
            (4, 0) => '\u24EA',
            (4, _) => (char)('\u2460' + digit - 1),
            (_, 0) => '\u2070',
            (_, 1) => '\u00B9',
            (_, <= 3) => (char)('\u00B0' + digit),
            _ => (char)('\u2070' + digit),
        };
    }
}

/// <summary>An identifier as the text holds it, and its letters and digits in ASCII capitals.</summary>
internal sealed record Planted(string Written, string Skeleton);
