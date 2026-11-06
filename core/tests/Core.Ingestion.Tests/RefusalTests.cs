namespace Core.Ingestion.Tests;

/// <summary>
/// The words of a refusal are made of the form, the code, the cause, the positions and the fields, and nothing else; each of them is worded as the person who corrects the material reads it.
/// </summary>
public sealed class RefusalTests
{
    private const string Whole = "lote recusado como documento: ";

    [Fact]
    public void ARefusalOfTheBatchAsAWholeHasNoPlaceToPointAt()
    {
        var refusal = Refusal.Whole(MaterialForm.Document, RefusalCode.NoUnits, null);

        Assert.Equal(($"{Whole}o material não tem nenhuma unidade", 0), (refusal.Reason, refusal.Findings.Count));
    }

    [Fact]
    public void TwentyFindingsAreShownAllAndTwentyOneAreShownTwentyAndCounted()
    {
        var twenty = Enumerable.Range(0, 20).Select(position => new Finding(position, MaterialField.Text)).ToList();
        var more = new Refusal(MaterialForm.Document, RefusalCode.FieldMissing, null, [.. twenty, new Finding(20, MaterialField.Text)]);

        var all = new Refusal(MaterialForm.Document, RefusalCode.FieldMissing, null, twenty);

        Assert.EndsWith("unidade 20, campo texto", all.Reason, StringComparison.Ordinal);
        Assert.EndsWith("unidade 20, campo texto; e mais 1", more.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RefusalCode.NoUnits, "o material não tem nenhuma unidade")]
    [InlineData(RefusalCode.FieldMissing, "falta o que a forma exige")]
    [InlineData(RefusalCode.FieldNotAllowed, "há o que a forma não admite")]
    [InlineData(RefusalCode.OrderNotDetermined, "duas mensagens têm o mesmo instante, e a ordem não pode vir da posição no arquivo")]
    [InlineData(RefusalCode.PassageOutsideText, "um trecho começa antes do texto, termina depois dele ou não tem extensão")]
    [InlineData(RefusalCode.ConversationThread, "o material traz fio de conversa, e a forma é de uma manifestação isolada")]
    [InlineData(RefusalCode.AuthorshipByPart, "o material tem autoria por trecho, e a forma não tem autoria por parte")]
    [InlineData(RefusalCode.PassageSplitsPair, "um limite de trecho cai dentro de um par substituto, e o trecho não seria o pretendido")]
    public void EachCodeIsWordedAsItIs(RefusalCode code, string words) =>
        Assert.Equal($"{Whole}{words}", Refusal.Whole(MaterialForm.Document, code, null).Reason);

    [Theory]
    [InlineData(MaterialForm.Conversation, "conversa")]
    [InlineData(MaterialForm.Comment, "comentário")]
    [InlineData(MaterialForm.Document, "documento")]
    [InlineData(MaterialForm.LabeledSequence, "sequência rotulada")]
    public void EachFormIsNamedAsItIs(MaterialForm form, string name) =>
        Assert.StartsWith($"lote recusado como {name}: ", Refusal.Whole(form, RefusalCode.NoUnits, null).Reason, StringComparison.Ordinal);

    [Theory]
    [InlineData(MaterialField.Text, "texto")]
    [InlineData(MaterialField.Author, "autor")]
    [InlineData(MaterialField.Instant, "instante")]
    [InlineData(MaterialField.RepliesTo, "a quem responde")]
    [InlineData(MaterialField.Passages, "trechos")]
    public void EachFieldIsNamedAsItIs(MaterialField field, string name)
    {
        var refusal = new Refusal(MaterialForm.Document, RefusalCode.FieldMissing, null, [new Finding(0, field)]);

        Assert.EndsWith($"unidade 1, campo {name}", refusal.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(UnreadableReason.Corrupted, "arquivo danificado ou de outro formato")]
    [InlineData(UnreadableReason.Truncated, "arquivo cortado antes do fim")]
    [InlineData(UnreadableReason.UnsupportedFormat, "formato que esta versão não lê")]
    [InlineData(UnreadableReason.OnlyPartlyRead, "só uma parte foi lida")]
    public void EachCauseOfAnUnreadableMaterialIsNamedAndPartialReadingIsNeverAccepted(UnreadableReason cause, string words) =>
        Assert.Equal(
            $"{Whole}a leitura não foi completa ({words}), e leitura parcial não é aceita",
            Refusal.Whole(MaterialForm.Document, RefusalCode.Unreadable, cause).Reason);

    [Fact]
    public void AnUnreadableMaterialWithNoCauseSaysSo() =>
        Assert.Equal(
            $"{Whole}a leitura não foi completa (motivo não informado), e leitura parcial não é aceita",
            Refusal.Whole(MaterialForm.Document, RefusalCode.Unreadable, null).Reason);

    [Fact]
    public void AValueOutsideTheEnumerationsIsADefectAndTheWordsSayWhich()
    {
        var form = Assert.Throws<ArgumentOutOfRangeException>(() => Refusal.Whole((MaterialForm)99, RefusalCode.NoUnits, null).Reason);
        var field = Assert.Throws<ArgumentOutOfRangeException>(() => new Refusal(MaterialForm.Document, RefusalCode.NoUnits, null, [new Finding(0, (MaterialField)99)]).Reason);
        var code = Assert.Throws<ArgumentOutOfRangeException>(() => Refusal.Whole(MaterialForm.Document, (RefusalCode)99, null).Reason);

        Assert.StartsWith("The form is not one of the four.", form.Message, StringComparison.Ordinal);
        Assert.StartsWith("The field is not one of the enumeration.", field.Message, StringComparison.Ordinal);
        Assert.StartsWith("The code is not one of the enumeration.", code.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFindingOfAWholeMaterialOrOfAUnitIsWordedWithoutTheMissingParts()
    {
        var start = $"{Whole}o material não tem nenhuma unidade: ";
        var whole = new Refusal(MaterialForm.Document, RefusalCode.NoUnits, null, [new Finding(null, null)]);
        var unit = new Refusal(MaterialForm.Document, RefusalCode.NoUnits, null, [new Finding(2, null)]);
        var field = new Refusal(MaterialForm.Document, RefusalCode.NoUnits, null, [new Finding(null, MaterialField.Instant)]);

        Assert.Equal([start + "o lote todo", start + "unidade 3", start + "campo instante"], [whole.Reason, unit.Reason, field.Reason]);
    }

    [Fact]
    public void ARefusalWithoutItsFindingsIsADefectOfTheCaller() =>
        Assert.Equal("findings", Assert.Throws<ArgumentNullException>(() => new Refusal(MaterialForm.Document, RefusalCode.NoUnits, null, null!)).ParamName);
}
