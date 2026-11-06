using Core.Boundary.Text;

namespace Core.Ingestion.Tests;

/// <summary>
/// The order of a conversation (issue 77, criteria 2 and 3): the moments, then the answers where two messages share a moment; nothing in the file decides,
/// and the refusals name the positions and the fields, never a word of the text.
/// </summary>
public sealed class ConversationOrderTests
{
    private const string Wording = "lote recusado como conversa:";
    private const string Undecided = "duas mensagens têm o mesmo instante, e nada no conteúdo decide qual vem antes: a ordem não pode vir da posição no arquivo";
    private const string Incoherent = "a ordem que as respostas dão contradiz os instantes ou a si mesma: uma resposta é anterior à mensagem que responde, ou as respostas formam um ciclo";
    private static readonly FormConference Conference = new(new TextBoundaries());

    private static ReadUnit At(string text, int second, int? answers) => new(text, "ana", Units.First.AddSeconds(second), answers, []);

    private static Refusal Refused(params ReadUnit[] units) => Assert.IsType<Refusal>(Conference.Confer(MaterialForm.Conversation, Units.Read(units)).Refusal);

    private static string Places(Refusal refusal) => string.Join(',', refusal.Findings.Select(finding => finding.Unit));

    private static string[] Texts(params ReadUnit[] units) =>
        [.. Assert.IsType<Conversation>(Conference.Confer(MaterialForm.Conversation, Units.Read(units)).Accepted).Messages.Select(message => message.Text)];

    [Fact]
    public void TwoMessagesWithTheSameMomentAreOrderedByTheAnswerWhateverTheirPlaceInTheFile()
    {
        var answerFirstInTheFile = Texts(At("RESPOSTA", 5, 1), At("PERGUNTA", 5, null));
        var questionFirstInTheFile = Texts(At("PERGUNTA", 5, null), At("RESPOSTA", 5, 0));

        Assert.Equal(["PERGUNTA", "RESPOSTA"], answerFirstInTheFile);
        Assert.Equal(["PERGUNTA", "RESPOSTA"], questionFirstInTheFile);
    }

    [Fact]
    public void ASequenceOfAnswersAtTheSameMomentIsOrderedFromTheFirstAnsweredToTheLast()
    {
        var third = At("TERCEIRA", 5, 2);
        var first = At("PRIMEIRA", 5, null);
        var second = At("SEGUNDA", 5, 1);

        Assert.Equal(["PRIMEIRA", "SEGUNDA", "TERCEIRA"], Texts(third, first, second));
    }

    [Fact]
    public void TwoAnswersAtTheSameMomentOfTheSameMessageAreNotOrderedByTheAnswers()
    {
        var refusal = Refused(At("ORIGEM", 5, null), At("UMA", 5, 0), At("OUTRA", 5, 0), At("DEPOIS", 9, null));

        Assert.Equal(
            (RefusalCode.OrderNotDetermined, "0,1,2"),
            (refusal.Code, Places(refusal)));
    }

    [Fact]
    public void AnAnswerThroughAMessageOfAnotherMomentStillOrdersTheTwoThatShareOne()
    {
        var read = new[] { At("NETA", 5, 1), At("FILHA", 3, 2), At("AVO", 1, null) };

        Assert.Equal(["AVO", "FILHA", "NETA"], Texts(read));
    }

    [Fact]
    public void WhereNothingInTheContentDecidesTheRefusalNamesEveryMessageOfTheGroupAndNoOther()
    {
        var refusal = Refused(At("UM", 1, null), At("DOIS", 5, null), At("TRES", 5, null), At("QUATRO", 7, null), At("CINCO", 7, 3));

        Assert.Equal(
            (RefusalCode.OrderNotDetermined, "1,2", $"{Wording} {Undecided}: unidade 2, campo instante; unidade 3, campo instante"),
            (refusal.Code, Places(refusal), refusal.Reason));
    }

    [Fact]
    public void AnAnswerWithAMomentBeforeTheMessageItAnswersIsRefusedAtTheAnswer()
    {
        var refusal = Refused(At("PERGUNTA", 9, null), At("RESPOSTA", 3, 0));

        Assert.Equal(
            (RefusalCode.OrderIncoherent, "1", $"{Wording} {Incoherent}: unidade 2, campo a mensagem respondida"),
            (refusal.Code, Places(refusal), refusal.Reason));
    }

    [Fact]
    public void AnAnswerAtTheMomentOfTheMessageItAnswersIsNotIncoherent() =>
        Assert.Equal(["PERGUNTA", "RESPOSTA"], Texts(At("RESPOSTA", 4, 1), At("PERGUNTA", 4, null)));

    [Fact]
    public void AnswersThatGoRoundInACycleAreRefusedAtEveryMessageOfTheCycleAndAtNoOther()
    {
        var refusal = Refused(At("A", 5, 1), At("B", 5, 2), At("C", 5, 0), At("FORA", 5, 0));

        Assert.Equal(
            (RefusalCode.OrderIncoherent, "0,1,2"),
            (refusal.Code, Places(refusal)));
    }

    [Fact]
    public void AnIncoherentOrderIsToldBeforeAnUndecidedOne()
    {
        var refusal = Refused(At("A", 5, 1), At("B", 5, 0));

        Assert.Equal(RefusalCode.OrderIncoherent, refusal.Code);
    }

    [Fact]
    public void AMissingFieldAndAnAnswerToNowhereAreToldBeforeTheOrder()
    {
        var missing = Refused(At("A", 5, 1), new ReadUnit("B", null, Units.First, 0, []));
        var dangling = Refused(At("A", 5, 7), At("B", 5, null));

        Assert.Equal((RefusalCode.FieldMissing, RefusalCode.FieldNotAllowed), (missing.Code, dangling.Code));
    }

    [Fact]
    public void TheReasonOfAnOrderRefusalNamesNoWordOfTheMessages()
    {
        var refusal = Refused(At("SEGREDO-ZQX", 5, null), At("SEGREDO-ZQX", 5, null));

        Assert.DoesNotContain("ZQX", refusal.Reason, StringComparison.Ordinal);
    }
}
