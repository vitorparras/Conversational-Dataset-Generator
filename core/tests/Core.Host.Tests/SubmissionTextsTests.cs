using Core.Host.Screens;

namespace Core.Host.Tests;

/// <summary>
/// The words of the screen of the submission (#607): each code of the barrier, of the conference, of the warning and of the gate the contract of #75 answers
/// has words of its own, and a code the screen does not know falls back on words that do not repeat it. No text is built from anything but the code.
/// </summary>
public sealed class SubmissionTextsTests
{
    private const string UnknownCode = "zqx_codigo_6070";

    /// <summary>The codes of <c>SubmissionRefusal</c> and of <c>RefusalCode</c> in Core.Ingestion, as the service of #75 writes them; Core.Host reaches that module only with the service.</summary>
    private static readonly string[] Codes =
    [
        "too_large", "unknown_format", "format_not_confirmed", "unreadable", "no_units", "field_missing", "field_not_allowed", "order_not_determined",
        "passage_outside_text", "conversation_thread", "authorship_by_part", "passage_splits_pair", "order_incoherent",
    ];

    private static readonly string[] FormValues = ["conversation", "comment", "document", "labeled-sequence"];

    public static TheoryData<string> Reasons() => [.. Codes];

    [Theory]
    [MemberData(nameof(Reasons))]
    public void EveryReasonOfTheBarrierAndOfTheConferenceHasWordsOfItsOwn(string code) =>
        Assert.NotEqual(SubmissionTexts.ReasonOf(UnknownCode), SubmissionTexts.ReasonOf(code));

    [Fact]
    public void NoTwoReasonsShareTheirWords()
    {
        var said = Codes.Select(SubmissionTexts.ReasonOf).ToList();

        Assert.Equal(said.Count, said.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("text")]
    [InlineData("author")]
    [InlineData("instant")]
    [InlineData("replies_to")]
    [InlineData("passages")]
    public void EveryPartOfAUnitHasItsName(string field) =>
        Assert.DoesNotContain("não nomeado", SubmissionTexts.WhereOf(0, field), StringComparison.Ordinal);

    [Theory]
    [InlineData("recovery_not_rehearsed")]
    [InlineData("recovery_rehearsal_stale")]
    public void EachCodeOfTheClosedGateSaysWhatIsMissing(string code) =>
        Assert.NotEqual(SubmissionTexts.BlockedOf(UnknownCode), SubmissionTexts.BlockedOf(code));

    [Fact]
    public void TheNameWithAPathIsAWarningOfItsOwnThatSaysTheSendingWentOn()
    {
        var words = SubmissionTexts.WarningOf("name_with_path");

        Assert.NotEqual(SubmissionTexts.WarningOf(UnknownCode), words);
        Assert.Contains("não foi recusado", words, StringComparison.Ordinal);
    }

    [Fact]
    public void ACodeTheScreenDoesNotKnowIsNeverRepeated()
    {
        var said = string.Join(
            '\n',
            SubmissionTexts.ReasonOf(UnknownCode),
            SubmissionTexts.WarningOf(UnknownCode),
            SubmissionTexts.BlockedOf(UnknownCode),
            SubmissionTexts.FieldErrorOf(UnknownCode, UnknownCode),
            SubmissionTexts.WhereOf(1, UnknownCode));

        Assert.DoesNotContain("zqx", said, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null, null, "No material inteiro")]
    [InlineData(0, null, "Na unidade 1")]
    [InlineData(2, "author", "Na unidade 3, campo autor")]
    [InlineData(null, "replies_to", "No campo a quem responde")]
    public void APointCountsTheUnitsFromOneAndNamesThePart(int? unit, string? field, string expected) =>
        Assert.Equal(expected, SubmissionTexts.WhereOf(unit, field));

    [Theory]
    [InlineData("condition", "missing", "Escolha a condição sob a qual o material entra.")]
    [InlineData("condition", "unknown", "Essa condição não está na lista desta instalação. Escolha uma das condições abaixo.")]
    [InlineData("origin", "missing", "Diga de onde o material veio, ou marque que a origem é desconhecida.")]
    [InlineData("origin", "conflict", "Escreva a origem ou marque que ela é desconhecida, não os dois.")]
    [InlineData("origin", "too_long", "A origem passa do tamanho aceito. Diga-a em menos palavras.")]
    [InlineData("file", "missing", "Escolha o arquivo do material.")]
    [InlineData("domain", "missing", "Escolha o domínio do material.")]
    [InlineData("declared_form", "invalid", "Escolha a forma do material.")]
    [InlineData("source_format", "missing", "Escolha o formato do arquivo.")]
    public void EachFieldSaysWhatToCorrectByItsCode(string field, string code, string expected) =>
        Assert.Equal(expected, SubmissionTexts.FieldErrorOf(field, code));

    [Theory]
    [InlineData(10_485_760, "10 MiB")]
    [InlineData(1_536, "1,5 KiB")]
    [InlineData(512, "512 bytes")]
    public void TheLimitIsSaidInTheUnitAPersonReads(long bytes, string expected) =>
        Assert.Equal(expected, SubmissionTexts.SizeOf(bytes));

    [Fact]
    public void TheFormsOfferedAreTheFourFormsOfMaterialAsTheContractNamesThem() =>
        Assert.Equal(FormValues, SubmissionTexts.Forms.Select(option => option.Value));
}
