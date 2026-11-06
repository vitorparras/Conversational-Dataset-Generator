namespace Core.Ingestion.Tests;

/// <summary>
/// The conference of the form declared: each form has a case that holds and at least one that is refused, and the reason of the refusal is in the result and names no word of the material.
/// </summary>
public sealed class FormConferenceTests
{
    private const string Secret = "ZQXSEGREDO";
    private const string Wording = "lote recusado como";
    private static readonly FormConference Conference = FormConference.Minimal();

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
    public void AConversationKeepsWhomAMessageAnswers()
    {
        var unit = new ReadUnit("UM", "ana", Units.First, "bia", []);

        var conversation = Assert.IsType<Conversation>(Conference.Confer(MaterialForm.Conversation, Units.Read(unit)).Accepted);

        var message = conversation.Messages[0];
        Assert.Equal(("ana", Units.First, "bia", "UM"), (message.Author, message.Instant, message.RepliesTo, message.Text));
    }

    [Fact]
    public void AConversationWhereAMessageHasNoAuthorIsRefusedNamingTheMessageAndTheField()
    {
        var read = Units.Read(Units.Message($"UM {Secret}", "ana", 1), Units.Message($"DOIS {Secret}", null, 2), Units.Message($"TRES {Secret}", "  ", 3));

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
        var refusal = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Comment, Units.Read(Units.Message(Secret, null, 1))).Refusal);

        Assert.Equal($"{Wording} comentário: falta o que a forma exige: unidade 1, campo autor", refusal.Reason);
    }

    [Fact]
    public void ACommentWithMoreThanOneUnitIsRefusedNamingTheExtraOnes()
    {
        var read = Units.Read(Units.Message("A", "ana", 1), Units.Message("B", "bia", 2), Units.Message("C", "cai", 3));

        var refusal = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Comment, read).Refusal);

        Assert.Equal((RefusalCode.MoreThanOneUnit, $"{Wording} comentário: a forma é de uma unidade só e o material tem mais: unidade 2; unidade 3"), (refusal.Code, refusal.Reason));
    }

    [Fact]
    public void ACommentThatAnswersSomeoneIsRefusedBecauseThatIsAThread()
    {
        var unit = new ReadUnit(Secret, "ana", null, "bia", []);

        var refusal = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Comment, Units.Read(unit)).Refusal);

        Assert.Equal($"{Wording} comentário: há o que a forma não admite: unidade 1, campo a quem responde", refusal.Reason);
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

        Assert.Equal((RefusalCode.FieldNotAllowed, $"{Wording} documento: há o que a forma não admite: unidade 2, campo autor"), (refusal.Code, refusal.Reason));
    }

    [Fact]
    public void ADocumentWithABlankSectionOrAnAnsweredAuthorOrPassagesIsRefused()
    {
        var answers = new ReadUnit("A", null, null, "bia", []);
        var blank = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Document, Units.Read(Units.Plain(" "))).Refusal);
        var answered = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Document, Units.Read(answers)).Refusal);
        var marked = Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Document, Units.Read(Units.Labeled("A", new Passage(0, 1)))).Refusal);

        Assert.Equal(
            new[]
            {
                $"{Wording} documento: falta o que a forma exige: unidade 1, campo texto",
                $"{Wording} documento: há o que a forma não admite: unidade 1, campo a quem responde",
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
        var read = Units.Read(Units.Labeled("ABCDE", new Passage(0, 2)), Units.Plain(Secret));

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
    public void TheToStringOfWhatIsReadAndAcceptedTellsNothingOfTheText()
    {
        var passages = new[] { new Passage(0, 1) };
        var unit = new ReadUnit(Secret, Secret, Units.First, Secret, passages);
        var shown = string.Concat(
            unit,
            new Message(Secret, Units.First, null, Secret),
            new Conversation([new Message(Secret, Units.First, Secret, Secret)]),
            new Comment(Secret, null, Secret),
            new Document([Secret]),
            new LabeledText(Secret, passages),
            new LabeledSequence([new LabeledText(Secret, passages)]));

        Assert.DoesNotContain(Secret, shown, StringComparison.Ordinal);
    }

    [Fact]
    public void TheConferenceAsksForExactlyOneRuleForEachFormAndRefusesTheRest()
    {
        var rules = AllForms();

        var missing = Assert.Throws<ArgumentException>(() => new FormConference(rules.Take(3)));
        var twice = Assert.Throws<ArgumentException>(() => new FormConference(rules.Append(new FixedRule(MaterialForm.Document))));
        var absent = Assert.Throws<ArgumentNullException>(() => new FormConference(null!));
        var nothing = Assert.Throws<ArgumentNullException>(() => new FormConference([null!]));

        Assert.Equal(["rules", "rules", "rules", "rule"], [missing.ParamName, twice.ParamName, absent.ParamName, nothing.ParamName]);
    }

    [Fact]
    public void ARuleThatIsGivenReplacesTheMinimalOneOfItsForm()
    {
        var result = new FormConference(AllForms()).Confer(MaterialForm.Comment, Units.Read(Units.Message("A", "ana", 1)));

        Assert.Equal(RefusalCode.FieldNotAllowed, result.Refusal!.Code);
    }

    [Fact]
    public void ADefectOfTheCallerIsNotAnOutcome()
    {
        var undefined = Assert.Throws<ArgumentOutOfRangeException>(() => Conference.Confer((MaterialForm)99, Units.Read()));
        var nothing = Assert.Throws<ArgumentNullException>(() => Conference.Confer(MaterialForm.Document, null!));
        var reason = Assert.Throws<ArgumentOutOfRangeException>(() => ReadOutcome.Unreadable((UnreadableReason)99));
        var material = Assert.Throws<ArgumentNullException>(() => ReadOutcome.Read(null!));
        var units = Assert.Throws<ArgumentNullException>(() => new ReadMaterial(null!));

        Assert.Equal(["declared", "outcome", "reason", "material", "units"], [undefined.ParamName, nothing.ParamName, reason.ParamName, material.ParamName, units.ParamName]);
    }

    [Fact]
    public void AReadOutcomeIsTheMaterialOrTheReasonAndNeverBoth()
    {
        var read = Units.Read(Units.Plain("A"));
        var unreadable = ReadOutcome.Unreadable(UnreadableReason.Truncated);

        Assert.Equal((true, null, null, UnreadableReason.Truncated), (read.Material is not null, read.Reason, unreadable.Material, unreadable.Reason));
    }

    [Fact]
    public void AUnitAndItsPartsRefuseWhatIsNull()
    {
        var text = Assert.Throws<ArgumentNullException>(() => new ReadUnit(null!, null, null, null, []));
        var passages = Assert.Throws<ArgumentNullException>(() => new ReadUnit("A", null, null, null, null!));
        var findings = Assert.Throws<ArgumentNullException>(() => new Refusal(MaterialForm.Document, RefusalCode.NoUnits, null, null!));
        var accepted = Assert.Throws<ArgumentNullException>(() => ConferenceResult.Accept(null!));
        var refused = Assert.Throws<ArgumentNullException>(() => ConferenceResult.Refuse(null!));

        Assert.Equal(["Text", "Passages", "findings", "material", "refusal"], [text.ParamName, passages.ParamName, findings.ParamName, accepted.ParamName, refused.ParamName]);
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

        public ConferenceResult Confer(ReadMaterial read) =>
            ConferenceResult.Refuse(new Refusal(Form, RefusalCode.FieldNotAllowed, null, []));
    }
}
