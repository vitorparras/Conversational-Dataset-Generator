using System.Diagnostics;
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
    [InlineData("CPF 100\u200B\u200B\u200B.200.300-00 ok", "CPF [documento] ok")]
    [InlineData("CPF \uFF11\uFF10\uFF10.200.300\u221200 ok", "CPF [documento] ok")]
    [InlineData("liga (00)\u200B 90000\u00B70000 ok", "liga [contato] ok")]
    [InlineData("liga +55 (00) 9\u200B0000-0000 ok", "liga [contato] ok")]
    [InlineData("manda zqx\u200B.teste@exem\u200Dplo.invalid ok", "manda [contato] ok")]
    [InlineData("placa ZQX\u200B-1A23 ok", "placa [documento] ok")]
    [InlineData("placa zqx\u200B1234 ok", "placa [documento] ok")]
    [InlineData("MAC 0A:1B:2C\u200B:3D:4E:5F ok", "MAC [documento] ok")]
    [InlineData("CPF 100\uFE0F200\uFE0F300\uFE0F00 ok", "CPF [documento] ok")]
    [InlineData("CPF 100\u034F200\u034F300\u034F00 ok", "CPF [documento] ok")]
    [InlineData("CPF 100\u180B200\u180B300\u180B00 ok", "CPF [documento] ok")]
    [InlineData("CPF 100\U000E0100200\U000E0100300\U000E010000 ok", "CPF [documento] ok")]
    [InlineData("CPF 100\u3164200\u1160300\uFFA000 ok", "CPF [documento] ok")]
    [InlineData("CPF 100\u2800200\u2800300\u280000 ok", "CPF [documento] ok")]
    [InlineData("CPF 100\u20DD200\u20DD300\u20DD00 ok", "CPF [documento] ok")]
    [InlineData("CPF 100\u2219200\u22C5300\u221500 ok", "CPF [documento] ok")]
    [InlineData("CPF 100\u2044200\u2044300\u204400 ok", "CPF [documento] ok")]
    [InlineData("CPF 100,200,300,00 ok", "CPF [documento] ok")]
    [InlineData("CPF 100;200;300;00 ok", "CPF [documento] ok")]
    [InlineData("CPF 100    200    300    00 ok", "CPF [documento] ok")]
    [InlineData("CPF 100\u2003\u2003\u2003\u2003200.300-00 ok", "CPF [documento] ok")]
    [InlineData("CPF \u2460\u24EA\u24EA.\u2461\u24EA\u24EA.\u2462\u24EA\u24EA-\u24EA\u24EA ok", "CPF [documento] ok")]
    [InlineData("CPF \u00B9\u2070\u2070.\u00B2\u2070\u2070.\u00B3\u2070\u2070-\u2070\u2070 ok", "CPF [documento] ok")]
    [InlineData("CPF 100, 200, 300-00 ok", "CPF [documento] ok")]
    [InlineData("CPF 100. 200. 300-00 ok", "CPF [documento] ok")]
    [InlineData("CPF 100&200&300&00 ok", "CPF [documento] ok")]
    [InlineData("CPF 100;200300;00 ok", "CPF 100;200300;00 ok")]
    [InlineData("CNPJ 10; 200; 300; 0001; 00 ok", "CNPJ [documento] ok")]
    [InlineData("IMEI 00    000000    000000    1 ok", "IMEI [documento] ok")]
    [InlineData("liga 00;0000;0000 ok", "liga [contato] ok")]
    [InlineData("liga (00) 90000;0000 e liga 00; 90000; 0000 ok", "liga [contato] e liga 00; 90000; 0000 ok")]
    [InlineData("liga +55    00    90000    0000 ok", "liga [contato] ok")]
    [InlineData("liga 00;90000;00\u206000 e 90000; 0000 e 10000, 2000 ok", "liga [contato] e 90000; 0000 e 10000, 2000 ok")]
    [InlineData("CPF 100;200;300;00;1 ok", "CPF [documento];1 ok")]
    [InlineData("CPF 100;200;300-00;5 ok", "CPF [documento];5 ok")]
    [InlineData("CPFs 100;200;300;00;400;500;600;00 ok", "CPFs [documento];[documento] ok")]
    [InlineData("IMEI 00;000000;000000;1;2 ok", "IMEI [documento];2 ok")]
    [InlineData("tabela 100    200    300    00    7 ok", "tabela [documento]    7 ok")]
    [InlineData("RG 10;200;300;0 e RG 10    200    300    0 ok", "RG [documento] e RG [documento] ok")]
    [InlineData("PIS 100;20030;00;0 ok", "PIS [documento] ok")]
    public void NumberSplitByZeroWidthOrOddSeparatorLeavesWithTheSeparators(string raw, string expected) =>
        Assert.Equal(expected, Removed(raw).Text);

    [Theory]
    [InlineData("liga (00) 90000-0000 ou 0000-0000 hoje", "liga [contato] ou [contato] hoje")]
    [InlineData("escreve para zqx.teste@exemplo.invalid amanhã", "escreve para [contato] amanhã")]
    [InlineData("a placa ZQX-1234 e a ZQX1A23 sumiram", "a placa [documento] e a [documento] sumiram")]
    [InlineData("o aparelho 0a-1b-2c-3d-4e-5f caiu", "o aparelho [documento] caiu")]
    [InlineData("liga 00 90000-0000 ok", "liga [contato] ok")]
    [InlineData("manda 10020030000@exemplo.invalid ok", "manda [contato] ok")]
    [InlineData("escreve zqx@exemplo.invalid.", "escreve [contato].")]
    [InlineData("zqx@exemplo.io fim", "[contato] fim")]
    [InlineData("manda \u200Bzqx@exemplo.invalid ok", "manda \u200B[contato] ok")]
    [InlineData("manda jo\u00E3o.silva@exemplo.invalid ok", "manda [contato] ok")]
    [InlineData("manda joa\u0303o.silva@exemplo.invalid ok", "manda [contato] ok")]
    [InlineData("manda zqx@ex\u00E9mplo.invalid ok", "manda [contato] ok")]
    [InlineData("manda zqx@exemplo.\u0440\u0444 ok", "manda [contato] ok")]
    public void ContactAndDeviceLeaveUnderTheirMarker(string raw, string expected) =>
        Assert.Equal(expected, Removed(raw).Text);

    [Theory]
    [InlineData("paguei R$ 12.345.678,90 por 1.500.000 litros, US$ 3.500.000 e € 25000000")]
    [InlineData("comprou 12345678 kg e 1 500 000 m² e 45% de 250 ml")]
    [InlineData("foi em 12/03/2024 às 10:30, ou 2024-03-12 15:45, ou 15/03/2024 - 10:30:15")]
    [InlineData("datas 12/03 15/03 20/03/24 e 31.12.2023 e 01-01-2024 14:00")]
    [InlineData("R$ \u200B\u200B 12.345.678,90 em 15/03/2024 23:59")]
    [InlineData("paguei US$ 1,234,567.89 e $1,234,567.89 e R$ 1 234 567,8")]
    [InlineData("aberto 08:00-12:00 e 14:00\u201318:00, hor\u00E1rio 10:30-11:45 e 08:00/12:00:30 fim")]
    [InlineData("de 01/03/2024-15/03/2024, de 01.03.2024-15.03.2024, de 01/03-15/03 e de 2024-03-01\u20132024-03-15")]
    [InlineData("safra 2019/20, de 2020 a 2024 e 15/03/2024 08:00-12:00")]
    public void MoneyQuantityAndDateStay(string raw)
    {
        var removed = Removed(raw);

        Assert.Equal((raw, 0), (removed.Text, removed.Report[0].Count));
    }

    [Theory]
    [InlineData("R$   12.345.678 fim", "R$   [documento] fim")]
    [InlineData("R$ 1234.567.890 fim", "R$ [documento] fim")]
    [InlineData("R$ 100-200-300 fim", "R$ [documento] fim")]
    [InlineData("R$ 100.200.3-0 fim", "R$ [documento] fim")]
    [InlineData("às 10:20:30:40 fim", "às [documento] fim")]
    [InlineData("em 15/03/2024 24:00 fim", "em [documento] fim")]
    [InlineData("em 15/03/2024 10:60 fim", "em [documento] fim")]
    [InlineData("nº 100.2000-0000 fim", "nº [documento] fim")]
    [InlineData("cartão 0000 1000 0000 fim", "cartão [documento] fim")]
    [InlineData("cartão 0000   1000   0000 fim", "cartão [documento] fim")]
    [InlineData("CPF 10020030000", "CPF [documento]")]
    [InlineData("c\u00F3digo x12345678", "c\u00F3digo x[documento]")]
    [InlineData("1000    20003000 fim", "1000    [documento] fim")]
    [InlineData("de 01/03/2024-15/13/2024 fim", "de [documento] fim")]
    [InlineData("de 2020-2024 fim", "de [contato] fim")]
    [InlineData("safra 2019/2020 fim", "safra [contato] fim")]
    public void NumberThatIsNoDateTimeMoneyNorQuantityLeaves(string raw, string expected) =>
        Assert.Equal(expected, Removed(raw).Text);

    [Theory]
    [InlineData("a reunião das 9 com 3 pessoas, sala 204, QZX7KW e kkkk @zqx #tbt")]
    [InlineData("nota 10, 9, 8, 7, 6, 5, 4, 3 e 1234567 e 2020-1 e zqx@local e ABC12")]
    [InlineData("código ZQXA5FC2B2D00453986F570ITEM e lote QZX12345678ZQX")]
    [InlineData("zz:yy:xx:ww:vv:uu e manda @exemplo.invalid no dia 12//2024")]
    [InlineData("A12345678B em mar-2024 e JAN2019, zqx@exemplo..invalid e zqx@exemplo.c0m")]
    [InlineData("Página 1 de 3\n2\n3\n4\n5\n6\n7\n8\n9 e a\u200Bb e x@y.z")]
    [InlineData("lotes 1200; 3400, 5600;\u200B 7800 e notas 9,5, 8,0, 7,5")]
    [InlineData("ganhei 1000!!! 2000?! anos 2023 \U0001F602\U0001F602 2024, 1000 + 2000 = 3000, | 2023 | 1500 | 3000 | e #1234 #5678")]
    [InlineData("ganhou 1000. 2000 ficaram, 1000\u2026 2000\u2026 e 10% 20% 30% 40% e nota 1000* 2000* 3000*")]
    [InlineData("\"1234\" \"5678\" e 1234 & 5678 e [12][13][14][15] e itens 12;13;14;15 e local -23.5505,-46.6333 fim")]
    [InlineData("anos 2019, 2020, 2021, 2022, tabela 1200    3400    5600 e itens 1000; 2000; 300 fim")]
    [InlineData("x\u00B2 e nota\u00B9 e \u2460 \u2461 \u2462 e 1\u00BD kg e \u2469\u246A\u246B\u246C\u246D\u246E\u246F\u2470")]
    [InlineData("fra\u00E7\u00F5es \u00BD \u2153 \u00BC \u2155 \u2159 \u215B \u2154 \u00BE e N = 6,022\u00B710\u00B2\u00B3 e c\u00F3digo A100;200;300;00B")]
    [InlineData("R$ 10; 1500; 2000 e R$ 100, 200, 300, 10 e pesos 10, 2000, 3000 kg e nos anos 90, 2000, 2010 e 2020")]
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
    public void OccurrenceBesideTheIdentifierLeavesUnderItsOwnMarker()
    {
        const string raw = "CPF100.200.300-00 fim";

        var sanitized = Sanitized(Sanitizer(new IdentifierStep(Boundaries), new DestinationsStep()), raw, Data(0, 3));

        Assert.Equal("[dado][documento] fim", sanitized.Text);
    }

    [Fact]
    public void PseudonymizedSpanTouchingTheIdentifierLeavesWithIt()
    {
        const string raw = "Zqxw100.200.300-00 fim Wvkt";

        var sanitized = Sanitized(Sanitizer(new IdentifierStep(Boundaries), new DestinationsStep()), raw, Pseudonymized(0, 8), Pseudonymized(23, 27));

        Assert.Equal(("[documento] fim Wvkt", 1), (sanitized.Text, sanitized.Pseudonyms.Count));
    }

    [Theory]
    [InlineData("\u200B", false)]
    [InlineData("1-", true)]
    [InlineData("1;", false)]
    [InlineData("1 ", true)]
    public void LongRunOfInvisibleCharactersOrDigitsIsReadInLinearTime(string unit, bool removed)
    {
        var single = Fastest(unit, 50_000, removed);

        var sixteenfold = Fastest(unit, 800_000, removed);

        Assert.InRange(sixteenfold / single, 0, 64);
    }

    /// <summary>
    /// The shortest of five readings, in milliseconds, of a run of the unit given repeated as many times as the length given, as invisible characters or digits joined by the
    /// writing of a number or of a list. Read in linear time, sixteen times the length takes about sixteen times as long, twice that once the run no longer fits the cache;
    /// read in quadratic time, 256 times. The run comes out whole, or under one marker when it is removed.
    /// </summary>
    private static double Fastest(string unit, int length, bool removed)
    {
        var raw = $"ok {string.Concat(Enumerable.Repeat(unit, length))} fim";
        var expected = removed ? $"ok [documento]{unit[1..]} fim" : raw;
        var readings = new List<double>();
        for (var reading = 0; reading < 5; reading++)
        {
            var clock = Stopwatch.StartNew();
            var sanitized = Removed(raw);
            readings.Add(clock.Elapsed.TotalMilliseconds);
            Assert.Equal(expected, sanitized.Text);
        }

        return readings.Min();
    }

    [Fact]
    public void NoBoundariesOrNoInputIsADefect()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new IdentifierStep(null!));
        _ = Assert.Throws<ArgumentNullException>(() => new IdentifierStep(Boundaries).Edits(null!));
    }
}
