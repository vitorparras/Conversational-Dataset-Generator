using Core.Ingestion.Sanitizing;
using static Core.Ingestion.Tests.Sanitizing.Sanitizing;

namespace Core.Ingestion.Tests.Sanitizing;

/// <summary>
/// The removal of the closed forms of identifier takes them out by their form, also when an invisible character or an odd separator splits them, with the separators that split
/// them, and leaves money, quantities and dates (issue 91). Every number here is synthetic: its check digits are wrong, its area code does not exist or its domain is reserved.
/// </summary>
public sealed class IdentifierStepTests
{
    private static SanitizedText Removed(string raw) => Sanitized(Sanitizer(new IdentifierStep(Boundaries)), raw);

    [Theory]
    [InlineData("meu CPF é 100.200.300-00, anota", "meu CPF é [documento], anota")]
    [InlineData("o CNPJ 10.200.300/0001-00 da firma", "o CNPJ [documento] da firma")]
    [InlineData("RG 10.200.300-0 emitido", "RG [documento] emitido")]
    [InlineData("agência 0001 conta 12345-6 fim", "agência 0001 conta [documento] fim")]
    [InlineData("IMEI 350000000000001 do aparelho", "IMEI [documento] do aparelho")]
    public void DocumentNumberWithUsualPunctuationLeaves(string raw, string expected)
    {
        var removed = Removed(raw);

        Assert.Equal((expected, "identificadores", 1), (removed.Text, removed.Report[0].Name, removed.Report[0].Count));
    }

    [Theory]
    [InlineData("CPF 100\u200B.200.300-00 ok", "CPF [documento] ok")]
    [InlineData("CPF 1\u200B0\u200C0\u2060200\uFEFF300\u00AD00 ok", "CPF [documento] ok")]
    [InlineData("CPF 100\u2024200\u2024300\u201100 ok", "CPF [documento] ok")]
    [InlineData("CPF 100 200   300 \t00 ok", "CPF [documento] ok")]
    [InlineData("CPF \uFF11\uFF10\uFF10.200.300\u221200 ok", "CPF [documento] ok")]
    [InlineData("liga (00)\u200B 90000\u00B70000 ok", "liga [contato] ok")]
    [InlineData("liga +55 (00) 9\u200B0000-0000 ok", "liga [contato] ok")]
    [InlineData("manda zqx\u200B.teste@exem\u200Dplo.invalid ok", "manda [contato] ok")]
    [InlineData("placa ZQX\u200B-1A23 ok", "placa [documento] ok")]
    [InlineData("placa zqx\u200B1234 ok", "placa [documento] ok")]
    [InlineData("MAC 0A:1B:2C\u200B:3D:4E:5F ok", "MAC [documento] ok")]
    public void NumberSplitByZeroWidthOrOddSeparatorLeavesWithTheSeparators(string raw, string expected) =>
        Assert.Equal(expected, Removed(raw).Text);

    [Theory]
    [InlineData("liga (00) 90000-0000 ou 0000-0000 hoje", "liga [contato] ou [contato] hoje")]
    [InlineData("escreve para zqx.teste@exemplo.invalid amanhã", "escreve para [contato] amanhã")]
    [InlineData("a placa ZQX-1234 e a ZQX1A23 sumiram", "a placa [documento] e a [documento] sumiram")]
    [InlineData("o aparelho 0a-1b-2c-3d-4e-5f caiu", "o aparelho [documento] caiu")]
    public void ContactAndDeviceLeaveUnderTheirMarker(string raw, string expected) =>
        Assert.Equal(expected, Removed(raw).Text);

    [Theory]
    [InlineData("paguei R$ 12.345.678,90 por 1.500.000 litros, US$ 3.500.000 e € 25000000")]
    [InlineData("comprou 12345678 kg e 1 500 000 m² e 45% de 250 ml")]
    [InlineData("foi em 12/03/2024 às 10:30, ou 2024-03-12 15:45, ou 15/03/2024 - 10:30:15")]
    [InlineData("datas 12/03 15/03 20/03/24 e 31.12.2023 e 01-01-2024 14:00")]
    public void MoneyQuantityAndDateStay(string raw)
    {
        var removed = Removed(raw);

        Assert.Equal((raw, 0), (removed.Text, removed.Report[0].Count));
    }

    [Theory]
    [InlineData("a reunião das 9 com 3 pessoas, sala 204, QZX7KW e kkkk @zqx #tbt")]
    [InlineData("nota 10, 9, 8, 7, 6, 5, 4, 3 e 1234567 e 2020-1 e zqx@local e ABC12")]
    [InlineData("código ZQXA5FC2B2D00453986F570ITEM e lote QZX12345678ZQX")]
    [InlineData("Página 1 de 3\n2\n3\n4\n5\n6\n7\n8\n9 e a\u200Bb e x@y.z")]
    public void TextWithoutIdentifierLeavesUnchanged(string raw)
    {
        var removed = Removed(raw);

        Assert.Equal(raw, removed.Text);
        Assert.Empty(removed.Report[0].Edits);
    }

    [Theory]
    [InlineData(4, 18, "CPF [documento] fim")]
    [InlineData(0, 18, "[documento] fim")]
    [InlineData(4, 11, "CPF [documento] fim")]
    [InlineData(15, 22, "CPF [documento]")]
    public void SpanMarkedAndRecognizedLeavesOnceWithoutNestedMarker(int start, int end, string expected)
    {
        const string raw = "CPF 100.200.300-00 fim";

        var sanitized = Sanitized(Sanitizer(new IdentifierStep(Boundaries), new DestinationsStep()), raw, Data(start, end));

        Assert.Equal((expected, 1, 0), (sanitized.Text, sanitized.Report[0].Count, sanitized.Report[1].Count));
    }

    [Fact]
    public void PseudonymizedSpanTouchingTheIdentifierLeavesWithIt()
    {
        const string raw = "Zqxw100.200.300-00 fim Wvkt";

        var sanitized = Sanitized(Sanitizer(new IdentifierStep(Boundaries), new DestinationsStep()), raw, Pseudonymized(0, 8), Pseudonymized(23, 27));

        Assert.Equal(("[documento] fim Wvkt", 1), (sanitized.Text, sanitized.Pseudonyms.Count));
    }

    [Fact]
    public void NoBoundariesOrNoInputIsADefect()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new IdentifierStep(null!));
        _ = Assert.Throws<ArgumentNullException>(() => new IdentifierStep(Boundaries).Edits(null!));
    }
}
