using Core.Boundary.Contracts;
using Core.Boundary.Text;

namespace Core.Ingestion.Tests;

/// <summary>
/// The conference of the form declared: each form has a case that holds and at least one that is refused, and the reason of the refusal is in the result and names no word of the material.
/// </summary>
public sealed class FormConferenceTests
{
    private const string Marker = "ZQXMARCADOR";
    private const string Wording = "lote recusado como";
    private static readonly FormConference Conference = new(new TextBoundaries());

    [Fact]
    public void AConversationWithAuthorAndMomentInEveryMessageIsAcceptedInTheOrderOfTheMoments()
    {
        var read = Units.Read(Units.Message("TRES", "ana", 30), Units.Message("UM", "bia", 10), Units.Message("DOIS", "ana", 20));

        var result = Conference.Confer(MaterialForm.Conversation, read);

        var conversation = Assert.IsType<Conversation>(result.Accepted);
        Assert.Equal(["UM", "DOIS", "TRES"], [.. conversation.Messages.Select(message => message.Text)]);
        Assert.Equal((MaterialForm.Conversation, null), (conversation.Form, result.Refusal));
    }

    [Fact]
    public void AConversationKeepsWhichMessageAMessageAnswersWhereTheOrderOfTheMomentsPutsIt()
    {
        var answered = new ReadUnit("DOIS", "ana", Units.First.AddSeconds(2), 1, []);
        var first = new ReadUnit("UM", "ana", Units.First, null, []);

        var conversation = Assert.IsType<Conversation>(Conference.Confer(MaterialForm.Conversation, Units.Read(answered, first)).Accepted);

        Assert.Equal(["UM", "DOIS"], [.. conversation.Messages.Select(message => message.Text)]);
        Assert.Equal([null, 0], [.. conversation.Messages.Select(message => message.RepliesTo)]);
    }

    [Fact]
    public void TwoMessagesOfTheSameSenderAnswerTheirOwnMessagesAndNotJustTheSender()
    {
        var read = Units.Read(
            Units.Message("A", "ana", 3),
            new ReadUnit("B", "bia", Units.First.AddSeconds(1), null, []),
            new ReadUnit("C", "bia", Units.First.AddSeconds(2), 0, []),
            new ReadUnit("D", "bia", Units.First.AddSeconds(4), 1, []));

        var messages = Assert.IsType<Conversation>(Conference.Confer(MaterialForm.Conversation, read).Accepted).Messages;

        Assert.Equal(["B", "C", "A", "D"], [.. messages.Select(message => message.Text)]);
        Assert.Equal([null, 2, null, 0], [.. messages.Select(message => message.RepliesTo)]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(2)]
    public void AnAnswerThatNamesNoOtherUnitOfTheMaterialIsRefusedAtTheUnitThatAnswers(int answered)
    {
        var read = Units.Read(Units.Message("A", "ana", 1), new ReadUnit("B", "bia", Units.First.AddSeconds(2), answered, []));

        var refusal = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Conversation, read).Refusal);

        Assert.Equal(
            (RefusalCode.FieldNotAllowed, $"{Wording} conversa: há o que a forma não admite: unidade 2, campo a mensagem respondida"),
            (refusal.Code, refusal.Reason));
    }

    [Fact]
    public void AConversationWhereAMessageHasNoAuthorIsRefusedNamingTheMessageAndTheField()
    {
        var read = Units.Read(Units.Message($"UM {Marker}", "ana", 1), Units.Message($"DOIS {Marker}", null, 2), Units.Message($"TRES {Marker}", "  ", 3));

        var refusal = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Conversation, read).Refusal);

        Assert.Equal(RefusalCode.FieldMissing, refusal.Code);
        Assert.Equal([1, 2], [.. refusal.Findings.Select(finding => finding.Unit!.Value)]);
        Assert.Equal($"{Wording} conversa: falta o que a forma exige: unidade 2, campo autor; unidade 3, campo autor", refusal.Reason);
    }

    [Fact]
    public void AConversationWhereAMessageHasNoMomentOrNoTextIsRefusedNamingEachField()
    {
        var read = Units.Read(Units.Message("UM", "ana", null), Units.Message("  ", "bia", 2));

        var refusal = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Conversation, read).Refusal);

        Assert.Equal($"{Wording} conversa: falta o que a forma exige: unidade 2, campo texto; unidade 1, campo instante", refusal.Reason);
    }

    [Fact]
    public void TwoMessagesWithTheSameMomentAreRefusedBecauseTheOrderWouldComeFromTheFile()
    {
        var read = Units.Read(Units.Message("A", "ana", 1), Units.Message("B", "bia", 5), Units.Message("C", "ana", 1));

        var refusal = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Conversation, read).Refusal);

        Assert.Equal(RefusalCode.OrderNotDetermined, refusal.Code);
        Assert.Equal([0, 2], [.. refusal.Findings.Select(finding => finding.Unit!.Value)]);
        Assert.Equal(
            $"{Wording} conversa: duas mensagens têm o mesmo instante, e a ordem não pode vir da posição no arquivo: unidade 1, campo instante; unidade 3, campo instante",
            refusal.Reason);
    }

    [Fact]
    public void AConversationWithMarkedPassagesIsRefusedAsNotAllowed()
    {
        var unit = new ReadUnit("UM", "ana", Units.First, null, [new Passage(0, 1)]);

        var refusal = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Conversation, Units.Read(unit)).Refusal);

        Assert.Equal((RefusalCode.FieldNotAllowed, $"{Wording} conversa: há o que a forma não admite: unidade 1, campo trechos"), (refusal.Code, refusal.Reason));
    }

    [Fact]
    public void ACommentWithAnAuthorIsAcceptedWithOrWithoutAMoment()
    {
        var withMoment = Assert.IsType<Comment>(Conference.Confer(MaterialForm.Comment, Units.Read(Units.Message("TEXTO", "ana", 4))).Accepted);
        var without = Assert.IsType<Comment>(Conference.Confer(MaterialForm.Comment, Units.Read(Units.Message("OUTRO", "bia", null))).Accepted);

        Assert.Equal(("ana", Units.First.AddSeconds(4), "TEXTO", MaterialForm.Comment), (withMoment.Author, withMoment.Instant, withMoment.Text, withMoment.Form));
        Assert.Equal(("bia", null), (without.Author, without.Instant));
    }

    [Fact]
    public void ACommentWithoutAnAuthorIsRefused()
    {
        var refusal = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Comment, Units.Read(Units.Message(Marker, null, 1))).Refusal);

        Assert.Equal($"{Wording} comentário: falta o que a forma exige: unidade 1, campo autor", refusal.Reason);
    }

    [Fact]
    public void ACommentWithMoreThanOneUnitIsRefusedAsAThreadNamingTheExtraOnes()
    {
        var read = Units.Read(Units.Message("A", "ana", 1), Units.Message("B", "bia", 2), Units.Message("C", "cai", 3));

        var refusal = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Comment, read).Refusal);

        Assert.Equal(
            (RefusalCode.ConversationThread, $"{Wording} comentário: o material traz fio de conversa, e a forma é de uma manifestação isolada: unidade 2; unidade 3"),
            (refusal.Code, refusal.Reason));
    }

    [Fact]
    public void ACommentThatAnswersSomeoneIsRefusedBecauseThatIsAThread()
    {
        var unit = new ReadUnit(Marker, "ana", null, 0, []);

        var refusal = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Comment, Units.Read(unit)).Refusal);

        Assert.Equal(
            (RefusalCode.ConversationThread, $"{Wording} comentário: o material traz fio de conversa, e a forma é de uma manifestação isolada: unidade 1, campo a mensagem respondida"),
            (refusal.Code, refusal.Reason));
    }

    [Fact]
    public void ACommentWithPassagesIsRefusedAsNotAllowed()
    {
        var unit = new ReadUnit("TEXTO", "ana", null, null, [new Passage(0, 2)]);

        var refusal = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Comment, Units.Read(unit)).Refusal);

        Assert.Equal((RefusalCode.FieldNotAllowed, $"{Wording} comentário: há o que a forma não admite: unidade 1, campo trechos"), (refusal.Code, refusal.Reason));
    }

    [Fact]
    public void ADocumentOfPlainTextIsAcceptedWithItsSectionsInTheOrderTheContentGives()
    {
        var accepted = Conference.Confer(MaterialForm.Document, Units.Read(Units.Plain("PRIMEIRO"), Units.Plain("SEGUNDO"))).Accepted;

        var document = Assert.IsType<Document>(accepted);
        Assert.Equal(MaterialForm.Document, document.Form);
        Assert.Equal(["PRIMEIRO", "SEGUNDO"], [.. document.Sections]);
    }

    [Fact]
    public void ADocumentWithAnAuthorForAPartIsRefused()
    {
        var read = Units.Read(Units.Plain("PRIMEIRO"), Units.Message("SEGUNDO", "ana", null));

        var refusal = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Document, read).Refusal);

        Assert.Equal(
            (RefusalCode.AuthorshipByPart, $"{Wording} documento: o material tem autoria por trecho, e a forma não tem autoria por parte: unidade 2, campo autor"),
            (refusal.Code, refusal.Reason));
    }

    [Fact]
    public void ADocumentWithABlankSectionOrAnAnsweredAuthorOrPassagesIsRefused()
    {
        var answers = new ReadUnit("A", null, null, 0, []);
        var blank = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Document, Units.Read(Units.Plain(" "))).Refusal);
        var answered = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Document, Units.Read(answers)).Refusal);
        var marked = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Document, Units.Read(Units.Labeled("A", new Passage(0, 1)))).Refusal);

        Assert.Equal(
            new[]
            {
                $"{Wording} documento: falta o que a forma exige: unidade 1, campo texto",
                $"{Wording} documento: há o que a forma não admite: unidade 1, campo a mensagem respondida",
                $"{Wording} documento: há o que a forma não admite: unidade 1, campo trechos",
            },
            new[] { blank.Reason, answered.Reason, marked.Reason });
    }

    [Fact]
    public void ALabeledSequenceWithPassagesInsideTheTextIsAccepted()
    {
        var read = Units.Read(Units.Labeled("ABCDE", new Passage(0, 2), new Passage(3, 5)), Units.Labeled("😀😀", new Passage(0, 2)));

        var sequence = Assert.IsType<LabeledSequence>(Conference.Confer(MaterialForm.LabeledSequence, read).Accepted);

        Assert.Equal(MaterialForm.LabeledSequence, sequence.Form);
        Assert.Equal(["ABCDE", "😀😀"], [.. sequence.Texts.Select(text => text.Text)]);
        Assert.Equal([2, 1], [.. sequence.Texts.Select(text => text.Passages.Count)]);
    }

    [Fact]
    public void ALabeledSequenceWhereAUnitHasNoPassageIsRefusedNamingTheUnit()
    {
        var read = Units.Read(Units.Labeled("ABCDE", new Passage(0, 2)), Units.Plain(Marker));

        var refusal = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.LabeledSequence, read).Refusal);

        Assert.Equal((RefusalCode.FieldMissing, $"{Wording} sequência rotulada: falta o que a forma exige: unidade 2, campo trechos"), (refusal.Code, refusal.Reason));
    }

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(0, 6)]
    public void ALabeledSequenceWherePassageFallsOutsideTheTextOrHasNoLengthIsRefused(int start, int end)
    {
        var read = Units.Read(Units.Labeled("ABCDE", new Passage(start, end)));

        var refusal = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.LabeledSequence, read).Refusal);

        Assert.Equal(RefusalCode.PassageOutsideText, refusal.Code);
        Assert.Equal(
            $"{Wording} sequência rotulada: um trecho começa antes do texto, termina depois dele ou não tem extensão: unidade 1, campo trechos",
            refusal.Reason);
    }

    [Fact]
    public void APassageIsMeasuredInTheIndexesOfTheTextAsTheReaderHoldsIt()
    {
        var inside = Conference.Confer(MaterialForm.LabeledSequence, Units.Read(Units.Labeled("😀😀", new Passage(0, 4))));
        var past = Conference.Confer(MaterialForm.LabeledSequence, Units.Read(Units.Labeled("😀😀", new Passage(0, 5))));

        Assert.Equal((true, RefusalCode.PassageOutsideText), (inside.Accepted is not null, past.Refusal!.Code));
    }

    [Theory]
    [InlineData(MaterialForm.Conversation)]
    [InlineData(MaterialForm.Comment)]
    [InlineData(MaterialForm.Document)]
    [InlineData(MaterialForm.LabeledSequence)]
    public void AMaterialWithNoUnitIsRefusedWhateverTheForm(MaterialForm form)
    {
        var refusal = Assert.IsType<Refusal>(Conference.Confer(form, Units.Read()).Refusal);

        Assert.Equal((RefusalCode.NoUnits, form, true), (refusal.Code, refusal.Form, refusal.Reason.Contains("nenhuma unidade", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(MaterialForm.Conversation, UnreadableReason.Corrupted, "arquivo danificado ou de outro formato")]
    [InlineData(MaterialForm.Comment, UnreadableReason.Truncated, "arquivo cortado antes do fim")]
    [InlineData(MaterialForm.Document, UnreadableReason.UnsupportedFormat, "formato que esta versão não lê")]
    [InlineData(MaterialForm.LabeledSequence, UnreadableReason.OnlyPartlyRead, "só uma parte foi lida")]
    public void AReadingThatIsNotWholeIsRefusedAndNoFormAcceptsIt(MaterialForm form, UnreadableReason reason, string words)
    {
        var result = Conference.Confer(form, ReadOutcome.Unreadable(reason));

        var refusal = Assert.IsType<Refusal>(result.Refusal);
        Assert.Equal((null, RefusalCode.Unreadable, reason), (result.Accepted, refusal.Code, refusal.Cause));
        Assert.Contains(words, refusal.Reason, StringComparison.Ordinal);
        Assert.Contains("leitura parcial não é aceita", refusal.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AResultIsOneOfTheTwoOutcomesAndNeverBoth()
    {
        var accepted = Conference.Confer(MaterialForm.Document, Units.Read(Units.Plain("A")));
        var refused = Conference.Confer(MaterialForm.Document, Units.Read());

        Assert.Equal((true, false, false, true), (accepted.Accepted is not null, accepted.Refusal is not null, refused.Accepted is not null, refused.Refusal is not null));
    }

    [Fact]
    public void MoreThanTwentyFindingsAreShownTwentyAndCounted()
    {
        var units = Enumerable.Range(0, 23).Select(_ => Units.Plain(" ")).ToArray();

        var refusal = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Document, Units.Read(units)).Refusal);

        Assert.Equal(23, refusal.Findings.Count);
        Assert.EndsWith("unidade 20, campo texto; e mais 3", refusal.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("unidade 21", refusal.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void FindingsOfAWholeMaterialOrOfAUnitAreWordedWithoutTheMissingParts()
    {
        var whole = new Refusal(MaterialForm.Document, RefusalCode.NoUnits, null, [new Finding(null, null)]);
        var unit = new Refusal(MaterialForm.Document, RefusalCode.NoUnits, null, [new Finding(2, null)]);
        var field = new Refusal(MaterialForm.Document, RefusalCode.NoUnits, null, [new Finding(null, MaterialField.Instant)]);
        var start = $"{Wording} documento: o material não tem nenhuma unidade: ";

        Assert.Equal(new[] { start + "o lote todo", start + "unidade 3", start + "campo instante" }, new[] { whole.Reason, unit.Reason, field.Reason });
    }

    [Fact]
    public void TheConferenceAsksForExactlyOneRuleForEachFormAndRefusesTheRest()
    {
        var rules = AllForms();

        var missing = Assert.Throws<ArgumentException>(() => new FormConference(rules.Take(3), new TextBoundaries()));
        var twice = Assert.Throws<ArgumentException>(() => new FormConference(rules.Append(new FixedRule(MaterialForm.Document)), new TextBoundaries()));
        var absent = Assert.Throws<ArgumentNullException>(() => new FormConference(null!, new TextBoundaries()));
        var nothing = Assert.Throws<ArgumentNullException>(() => new FormConference([null!], new TextBoundaries()));

        var unbounded = Assert.Throws<ArgumentNullException>(() => new FormConference(rules, null!));

        Assert.Equal(
            ["rules", "rules", "rules", "rule", "boundaries"],
            [missing.ParamName, twice.ParamName, absent.ParamName, nothing.ParamName, unbounded.ParamName]);
        Assert.StartsWith("There is not one rule for each of the four forms.", missing.Message, StringComparison.Ordinal);
        Assert.StartsWith("There are two rules for the form Document.", twice.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMinimalConferenceHasTheRuleOfEachFormAndEachRuleKnowsItsForm()
    {
        var rules = new IFormRule[] { new ConversationRule(), new CommentRule(), new DocumentRule(), new LabeledSequenceRule() };

        Assert.Equal(
            [MaterialForm.Conversation, MaterialForm.Comment, MaterialForm.Document, MaterialForm.LabeledSequence],
            rules.Select(rule => rule.Form).ToArray());
        Assert.All(rules, rule => Assert.Equal("read", Assert.Throws<ArgumentNullException>(() => rule.Confer(null!, new TextBoundaries())).ParamName));
    }

    [Fact]
    public void AnyPassageOutsideTheTextRefusesTheUnitEvenWhenAnotherOneIsInside()
    {
        var read = Units.Read(Units.Labeled("ABCDE", new Passage(0, 2), new Passage(0, 99)), Units.Labeled("ABCDE", new Passage(0, 99), new Passage(0, 2)));

        var refusal = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.LabeledSequence, read).Refusal);

        Assert.Equal([0, 1], [.. refusal.Findings.Select(finding => finding.Unit!.Value)]);
    }

    [Fact]
    public void TiedMessagesAreNamedInTheOrderOfTheUnitsWhateverTheGroupsAre()
    {
        var read = Units.Read(Units.Message("A", "ana", 5), Units.Message("B", "bia", 1), Units.Message("C", "ana", 5), Units.Message("D", "bia", 1));

        var refusal = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Conversation, read).Refusal);

        Assert.Equal([0, 1, 2, 3], [.. refusal.Findings.Select(finding => finding.Unit!.Value)]);
    }

    [Fact]
    public void ARuleThatIsGivenReplacesTheMinimalOneOfItsForm()
    {
        var result = new FormConference(AllForms(), new TextBoundaries()).Confer(MaterialForm.Comment, Units.Read(Units.Message("A", "ana", 1)));

        Assert.Equal(RefusalCode.FieldNotAllowed, result.Refusal!.Code);
    }

    [Fact]
    public void ADefectOfTheCallerIsNotAnOutcome()
    {
        var undefined = Assert.Throws<ArgumentOutOfRangeException>(() => Conference.Confer((MaterialForm)99, Units.Read()));
        var nothing = Assert.Throws<ArgumentNullException>(() => Conference.Confer(MaterialForm.Document, null!));

        Assert.Equal(["declared", "outcome"], [undefined.ParamName, nothing.ParamName]);
        Assert.StartsWith("The form is not one of the four.", undefined.Message, StringComparison.Ordinal);
    }

    private static IFormRule[] AllForms() =>
        [
            new FixedRule(MaterialForm.Document),
            new FixedRule(MaterialForm.Comment),
            new FixedRule(MaterialForm.Conversation),
            new FixedRule(MaterialForm.LabeledSequence),
        ];

    private sealed class FixedRule : IFormRule
    {
        public FixedRule(MaterialForm form)
        {
            Form = form;
        }

        public MaterialForm Form { get; }

        public ConferenceResult Confer(ReadMaterial read, ITextBoundaries boundaries) =>
            ConferenceResult.Refuse(new Refusal(Form, RefusalCode.FieldNotAllowed, null, []));
    }
}
