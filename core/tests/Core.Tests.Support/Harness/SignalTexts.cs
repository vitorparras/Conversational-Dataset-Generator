using CsCheck;

namespace Core.Tests.Support.Harness;

/// <summary>
/// Texts loaded with the marks of how people write: slang, misspelling, repeated letters, abbreviations, expressive punctuation, capitals, mixed case, emoji, symbols, a mix of
/// languages, typographic quotes, the shape of a character and irregular spacing. Each mark comes with its span, in code points, so that a property can say which one a step lost.
/// No piece holds a sequence a wrong encoding leaves, a page break or anything marked for removal.
/// </summary>
public static class SignalTexts
{
    public static string Slang { get; } = "gíria";
    public static string Misspelling { get; } = "erro de grafia";
    public static string Repetition { get; } = "letra repetida";
    public static string Abbreviation { get; } = "abreviação";
    public static string ExpressivePunctuation { get; } = "pontuação expressiva";
    public static string Capitals { get; } = "caixa alta";
    public static string MixedCase { get; } = "caixa mista";
    public static string Emoji { get; } = "emoji";
    public static string Symbol { get; } = "símbolo";
    public static string LanguageMix { get; } = "mistura de línguas";
    public static string TypographicQuotes { get; } = "aspas tipográficas";
    public static string CharacterShape { get; } = "forma do caractere";
    public static string Spacing { get; } = "espaçamento";

    private static readonly Dictionary<string, string[]> Pieces = new(StringComparer.Ordinal)
    {
        [Slang] = ["mano", "tlgd", "pdc", "mó", "suave", "kkkkkk", "rsrsrs", "top demais"],
        [Misspelling] = ["concerteza", "naum", "seje", "menas", "pobrema", "intendi", "agente vai"],
        [Repetition] = ["siiiim", "nããão", "booom", "aaaah", "éééé", "TÁÁÁ"],
        [Abbreviation] = ["q", "tb", "hj", "msg", "vlw", "pq", "vc"],
        [ExpressivePunctuation] = ["!!!", "?!?!", "...", "…", "!!11", "??", "?!"],
        [Capitals] = ["NÃO", "SOCORRO", "PARA COM ISSO", "ÓBVIO", "AMANHÃ", "AÇÃO"],
        [MixedCase] = ["ViU", "nUnCa", "Tá BoM", "sEi lÁ", "oBriGaDo"],
        [Emoji] = ["\U0001F602", "\U0001F525\U0001F525", "\U0001F44D\U0001F3FD", "❤\uFE0F", "\U0001F468\u200D\U0001F469\u200D\U0001F467", "\U0001F64F"],
        [Symbol] = ["→", "©", "%", "#tbt", "@zqx", "R$", "★", "°", "½", "™"],
        [LanguageMix] = ["so cute", "sorry", "whatever", "¿qué?", "danke", "日本", "cringe"],
        [TypographicQuotes] = ["“assim”", "‘ok’", "«não»", "—", "–"],
        [CharacterShape] = ["ＡＢＣ", "e\u0301", "a\u200Bb", "ﬁm", "x\u00A0y"],
    };

    private static readonly string[] Separators = [" ", "  ", "   ", "\t", " \t ", "\n", " \n  "];

    /// <summary>Every kind of mark but the spacing, which comes between any two pieces.</summary>
    public static IReadOnlyList<string> Kinds { get; } = [.. Pieces.Keys];

    /// <summary>Texts of one to twelve pieces of the kinds given, each followed by a run of spacing; every piece and every run is a mark.</summary>
    /// <param name="kinds">The kinds the pieces are drawn from.</param>
    public static Gen<SignalSample> Of(params string[] kinds)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        var piece = Gen.OneOf(kinds.SelectMany(kind => Pieces[kind].Select(text => new SignalMark(0, 0, text, kind))).ToArray());
        var separator = Gen.OneOf(Separators);
        return Gen.Select(piece, separator, (written, space) => (written, space)).Array[1, 12].Select(Joined);
    }

    private static SignalSample Joined((SignalMark Piece, string Space)[] parts)
    {
        var marks = new List<SignalMark>();
        var at = 0;
        foreach (var (piece, space) in parts)
        {
            marks.Add(MarkAt(piece.Text, piece.Kind, at));
            marks.Add(MarkAt(space, Spacing, marks[^1].End));
            at = marks[^1].End;
        }

        return new SignalSample(string.Concat(marks.Select(mark => mark.Text)), marks);
    }

    private static SignalMark MarkAt(string text, string kind, int at) => new(at, at + text.EnumerateRunes().Count(), text, kind);
}
