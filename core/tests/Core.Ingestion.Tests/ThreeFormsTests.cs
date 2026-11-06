using Core.Boundary.Contracts;
using Core.Boundary.Text;

namespace Core.Ingestion.Tests;

/// <summary>
/// The comment, the document and the labeled sequence refuse what they cannot hold, and say why in the result: a thread is never dropped to leave a comment, an author by part is never dropped
/// to leave a document, and a limit between the halves of a surrogate pair never gives the neighbouring passage (CA-1 to CA-5).
/// </summary>
public sealed class ThreeFormsTests
{
    private const string Marker = "ZQXMARCADOR";
    private const string Wording = "lote recusado como";
    private const string Thread = "o material traz fio de conversa, e a forma é de uma manifestação isolada";
    private const string ByPart = "o material tem autoria por trecho, e a forma não tem autoria por parte";
    private const string Splits = "um limite de trecho cai dentro de um par substituto, e o trecho não seria o pretendido";

    private static readonly FormConference Conference = new(new TextBoundaries());

    [Fact]
    public void ACommentWithNoAuthorIsRefusedNamingTheMissingAuthorOfTheUnit()
    {
        var blank = new ReadUnit(Marker, "  ", null, null, []);

        var none = Refused(MaterialForm.Comment, Units.Message(Marker, null, null));
        var empty = Refused(MaterialForm.Comment, blank);

        Assert.Equal((RefusalCode.FieldMissing, $"{Wording} comentário: falta o que a forma exige: unidade 1, campo autor"), (none.Code, none.Reason));
        Assert.Equal(none.Reason, empty.Reason);
    }

    [Fact]
    public void ACommentThatBringsAThreadByUnitsAndByAnswerIsRefusedWithBothPlaces()
    {
        var answers = new ReadUnit(Marker, "ana", null, "bia", []);
        var read = Units.Read(answers, Units.Message("B", "bia", 2), new ReadUnit("C", "cai", null, "ana", []));

        var refusal = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Comment, read).Refusal);

        Assert.Equal(
            (RefusalCode.ConversationThread, $"{Wording} comentário: {Thread}: unidade 2; unidade 3; unidade 1, campo a quem responde; unidade 3, campo a quem responde"),
            (refusal.Code, refusal.Reason));
    }

    [Fact]
    public void ACommentThatIsAThreadAndHasNoAuthorIsRefusedForTheAuthorFirst()
    {
        var refusal = Refused(MaterialForm.Comment, new ReadUnit(Marker, null, null, "bia", []));

        Assert.Equal(RefusalCode.FieldMissing, refusal.Code);
    }

    [Fact]
    public void AThreadIsNeverLeftAsAnAcceptedComment()
    {
        var thread = Units.Read(Units.Message("A", "ana", 1), Units.Message("B", "bia", 2));

        var result = Conference.Confer(MaterialForm.Comment, thread);

        Assert.Equal((null, RefusalCode.ConversationThread), (result.Accepted, result.Refusal!.Code));
    }

    [Fact]
    public void ADocumentWithAuthorshipByPartIsRefusedNamingEveryPartThatHasOne()
    {
        var read = new[] { Units.Message("A", "ana", null), Units.Plain("B"), Units.Message("C", "  ", null), Units.Message("D", "bia", 3) };

        var refusal = Refused(MaterialForm.Document, read);

        Assert.Equal(
            (RefusalCode.AuthorshipByPart, $"{Wording} documento: {ByPart}: unidade 1, campo autor; unidade 3, campo autor; unidade 4, campo autor"),
            (refusal.Code, refusal.Reason));
    }

    [Fact]
    public void ADocumentWithABlankPartIsRefusedForTheTextBeforeTheAuthorship()
    {
        var refusal = Refused(MaterialForm.Document, new ReadUnit(" ", "ana", null, null, []));

        Assert.Equal(RefusalCode.FieldMissing, refusal.Code);
    }

    [Fact]
    public void ADocumentWithAnAnsweredAuthorOrPassagesButNoAuthorStaysNotAllowed()
    {
        var answered = Refused(MaterialForm.Document, new ReadUnit("A", null, null, "bia", []));
        var marked = Refused(MaterialForm.Document, new ReadUnit("A", null, null, null, [new Passage(0, 1)]));

        Assert.Equal([RefusalCode.FieldNotAllowed, RefusalCode.FieldNotAllowed], [answered.Code, marked.Code]);
    }

    [Fact]
    public void ThePartsOfTheFormsHaveNoFieldForAnAuthorExceptTheComment()
    {
        var sections = typeof(Document).GetProperties().Select(property => property.Name);
        var texts = typeof(LabeledText).GetProperties().Select(property => property.Name);

        Assert.DoesNotContain(sections.Concat(texts), name => name.Contains("Author", StringComparison.Ordinal));
        Assert.Equal(typeof(string), typeof(Document).GetProperty("Sections")!.PropertyType.GetGenericArguments()[0]);
    }

    [Theory]
    [InlineData(0, 2, 0)]
    [InlineData(2, 4, 0)]
    [InlineData(2, 3, 0)]
    [InlineData(1, 2, 0)]
    public void APassageWithALimitBetweenTheHalvesOfAPairIsRefusedAndNoNeighbourIsGiven(int start, int end, int unit)
    {
        var read = Units.Read(Units.Labeled("A\U0001F642B", new Passage(start, end)));

        var result = Conference.Confer(MaterialForm.LabeledSequence, read);

        var refusal = Assert.IsType<Refusal>(result.Refusal);
        Assert.Equal(
            (null, RefusalCode.PassageSplitsPair, $"{Wording} sequência rotulada: {Splits}: unidade {unit + 1}, campo trechos"),
            (result.Accepted, refusal.Code, refusal.Reason));
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(0, 1)]
    [InlineData(3, 4)]
    [InlineData(0, 4)]
    public void APassageWithTheLimitsOutsideThePairIsAcceptedAndResolvesToTheSameText(int start, int end)
    {
        var read = Units.Read(Units.Labeled("A\U0001F642B", new Passage(start, end)));

        var sequence = Assert.IsType<LabeledSequence>(Conference.Confer(MaterialForm.LabeledSequence, read).Accepted);

        var only = sequence.Texts[0];
        Assert.Equal("A\U0001F642B"[start..end], only.Resolve(0));
        Assert.Equal(only.Resolve(0), only.Resolve(0));
    }

    [Fact]
    public void ALoneSurrogateIsNotAPairAndItsLimitsAreAccepted()
    {
        var read = Units.Read(Units.Labeled("\uD83DA", new Passage(0, 1), new Passage(1, 2)));

        var sequence = Assert.IsType<LabeledSequence>(Conference.Confer(MaterialForm.LabeledSequence, read).Accepted);

        Assert.Equal(["\uD83D", "A"], [sequence.Texts[0].Resolve(0), sequence.Texts[0].Resolve(1)]);
    }

    [Fact]
    public void APassageOutsideTheTextIsRefusedAsOutsideBeforeItsPairIsAsked()
    {
        var boundaries = new CountingBoundaries();
        var conference = new FormConference(boundaries);
        var read = Units.Read(Units.Labeled("A\U0001F642B", new Passage(0, 9)));

        var refusal = Assert.IsType<Refusal>(conference.Confer(MaterialForm.LabeledSequence, read).Refusal);

        Assert.Equal((RefusalCode.PassageOutsideText, 0), (refusal.Code, boundaries.Asked));
    }

    [Fact]
    public void BothLimitsOfEveryPassageAreAskedOfTheBoundary()
    {
        var boundaries = new CountingBoundaries();
        var conference = new FormConference(boundaries);
        var read = Units.Read(Units.Labeled("ABCD", new Passage(0, 1), new Passage(2, 3)));

        _ = conference.Confer(MaterialForm.LabeledSequence, read);

        Assert.Equal(["0", "1", "2", "3"], [.. boundaries.Indexes.Select(index => index.ToString(System.Globalization.CultureInfo.InvariantCulture))]);
    }

    [Fact]
    public void EveryFormHasACaseOfRefusalWithItsReasonInTheResult()
    {
        var refusals = new[]
        {
            Refused(MaterialForm.Comment, Units.Message(Marker, null, null)),
            Refused(MaterialForm.Document, Units.Message(Marker, "ana", null)),
            Refused(MaterialForm.LabeledSequence, Units.Plain(Marker)),
        };

        Assert.All(refusals, refusal => Assert.DoesNotContain(Marker, refusal.Reason, StringComparison.Ordinal));
        Assert.Equal(
            [MaterialForm.Comment, MaterialForm.Document, MaterialForm.LabeledSequence],
            [.. refusals.Select(refusal => refusal.Form)]);
        Assert.All(refusals, refusal => Assert.NotEmpty(refusal.Findings));
    }

    [Fact]
    public void ResolvingAPassageNeedsAPassageThatIsThere()
    {
        var text = new LabeledText("ABC", [new Passage(1, 3)]);

        var below = Assert.Throws<ArgumentOutOfRangeException>(() => text.Resolve(-1));
        var past = Assert.Throws<ArgumentOutOfRangeException>(() => text.Resolve(1));

        Assert.Equal(("BC", "position", "position"), (text.Resolve(0), below.ParamName, past.ParamName));
    }

    [Fact]
    public void TheRuleOfTheSequenceNeedsABoundary()
    {
        var defect = Assert.Throws<ArgumentNullException>(() => new FormConference(null!));

        Assert.Equal("boundaries", defect.ParamName);
    }

    private static Refusal Refused(MaterialForm form, params ReadUnit[] units) =>
        Assert.IsType<Refusal>(Conference.Confer(form, Units.Read(units)).Refusal);

    private sealed class CountingBoundaries : ITextBoundaries
    {
        private readonly TextBoundaries real = new();
        private readonly List<int> indexes = [];

        public int Asked => indexes.Count;

        public IReadOnlyList<int> Indexes => indexes;

        public bool SplitsPair(string text, int utf16Index)
        {
            indexes.Add(utf16Index);
            return real.SplitsPair(text, utf16Index);
        }
    }
}
