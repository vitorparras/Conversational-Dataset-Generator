using System.Text;
using Core.Platform.Keys;

namespace Core.Platform.Tests.Keys;

/// <summary>How the two custody files are written: the key as one line of base64, the labels one per line, and what each way of getting it wrong is called.</summary>
public sealed class CustodyFileFormatTests
{
    [Fact]
    public void LabelsOnePerLineAreTakenInOrderAndOnceEach()
    {
        var (labels, problem) = CustodyFileFormat.ParseLabels(Bytes("dominio-b\ndominio-a\ndominio-b\n"));

        Assert.Null(problem);
        Assert.Equal(["dominio-a", "dominio-b"], labels!.All);
    }

    [Theory]
    [InlineData("dominio-a\r\ndominio-b\r\n")]
    [InlineData("dominio-a\ndominio-b")]
    [InlineData("dominio-a\r\ndominio-b")]
    public void LabelsWithAnyOrNoLastBreakAreTheSame(string text) =>
        Assert.Equal(["dominio-a", "dominio-b"], CustodyFileFormat.ParseLabels(Bytes(text)).Labels!.All);

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void LabelsFileWithNoLabelGivesNone(string text)
    {
        var (labels, problem) = CustodyFileFormat.ParseLabels(Bytes(text));

        Assert.Null(problem);
        Assert.Empty(labels!.All);
    }

    [Theory]
    [InlineData("dominio-a\n\ndominio-b\n", 2)]
    [InlineData("\n\n", 1)]
    [InlineData("dominio-a\n dominio-b\n", 2)]
    [InlineData("dominio-a\ndominio-b \n", 2)]
    [InlineData("dominio-a\nmeio\rmeio\n", 2)]
    [InlineData("dominio-a\ndominio\tb\n", 2)]
    public void LabelThatIsEmptyOrHasSpaceOrControlNamesItsLine(string text, int line)
    {
        var (labels, problem) = CustodyFileFormat.ParseLabels(Bytes(text));

        Assert.Null(labels);
        Assert.Equal($"linha {line}: rótulo vazio, com espaço em volta ou com caractere de controle", problem);
    }

    [Fact]
    public void LabelsFileWithAByteOrderMarkIsRefusedBecauseTheFirstLabelWouldCarryIt()
    {
        var (labels, problem) = CustodyFileFormat.ParseLabels([.. Encoding.UTF8.GetPreamble(), .. Bytes("dominio-a\n")]);

        Assert.Null(labels);
        Assert.Equal("começa com a marca de ordem de bytes (BOM); grave UTF-8 sem ela", problem);
    }

    [Fact]
    public void LabelsFileThatIsNotUtf8IsRefused()
    {
        var (labels, problem) = CustodyFileFormat.ParseLabels([0x64, 0xFF, 0xFE, 0x0A]);

        Assert.Null(labels);
        Assert.Equal("não é UTF-8 válido", problem);
    }

    [Fact]
    public void LabelsInAccentedUtf8AreKeptAsWritten()
    {
        var (labels, problem) = CustodyFileFormat.ParseLabels(Bytes("domínio-ç\n"));

        Assert.Null(problem);
        Assert.Equal(["domínio-ç"], labels!.All);
    }

    [Fact]
    public void KeyContentIsZeroedOnceItWasRead()
    {
        var content = Bytes(Convert.ToBase64String(SyntheticKey.Valid()) + "\n");

        var (key, problem) = CustodyFileFormat.ParseKey(content);

        Assert.Null(problem);
        Assert.True(key.AsSpan().SequenceEqual(SyntheticKey.Valid()));
        Assert.All(content, value => Assert.Equal(0, value));
    }

    [Fact]
    public void KeyContentIsZeroedAlsoWhenItIsRefused()
    {
        var content = Bytes("isto-nao-e-base64!\n");

        var (key, problem) = CustodyFileFormat.ParseKey(content);

        Assert.Null(key);
        Assert.NotNull(problem);
        Assert.All(content, value => Assert.Equal(0, value));
    }

    [Theory]
    [InlineData("AAA", 3)]
    [InlineData("AAAA=AAA", 8)]
    public void KeyTextThatIsNotBase64NamesTheSizeOfTheTextOnly(string text, int size)
    {
        var (key, problem) = CustodyFileFormat.ParseKey(Bytes(text + "\n"));

        Assert.Null(key);
        Assert.Equal($"não é base64, {size} bytes", problem);
    }

    [Theory]
    [InlineData(" {0}")]
    [InlineData("{0} ")]
    [InlineData("{0}\t")]
    [InlineData("{0}\r\r")]
    [InlineData("AAAA AAAA")]
    public void KeyLineWithWhiteSpaceAnywhereIsRefusedEvenWhenTheBase64DecoderWouldIgnoreIt(string pattern)
    {
        var line = string.Format(System.Globalization.CultureInfo.InvariantCulture, pattern, Convert.ToBase64String(SyntheticKey.Valid()));

        var (key, problem) = CustodyFileFormat.ParseKey(Bytes(line + "\n"));

        Assert.Null(key);
        Assert.Equal("espaço em branco ou caractere de controle na linha", problem);
    }

    [Fact]
    public void KeyLineWithASpaceInTheMiddleIsRefusedAndItsValueIsNotShown()
    {
        var text = Convert.ToBase64String(SyntheticKey.Valid());
        var spaced = $"{text[..10]} {text[10..]}";

        var (key, problem) = CustodyFileFormat.ParseKey(Bytes(spaced));

        Assert.Null(key);
        Assert.DoesNotContain(text[..10], problem!, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyFileThatIsOnlyABreakIsEmpty() =>
        Assert.Equal("arquivo vazio", CustodyFileFormat.ParseKey(Bytes("\r\n")).Problem);

    [Fact]
    public void KeyFileOfTwoLinesCountsTheLines() =>
        Assert.Equal("2 linhas, esperada 1", CustodyFileFormat.ParseKey(Bytes("AAAA\nAAAA\n")).Problem);

    private static byte[] Bytes(string text) => new UTF8Encoding(false).GetBytes(text);

    [Fact]
    public void EpochsOnePerLineAreReadWithTheirNumbersAndSecrets()
    {
        var (epochs, problem) = CustodyFileFormat.ParseEpochs(Bytes($"2:{Secret(2)}\n1:{Secret(1)}\n"));

        Assert.Null(problem);
        Assert.Equal<IEnumerable<int>>([1, 2], epochs!.Numbers());
        Assert.Equal((2, true), (epochs.Newest, epochs.Holds(1) && epochs.Holds(2)));
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("")]
    public void EpochsWithAnyOrNoLastBreakAreTheSame(string ending) =>
        Assert.Equal<IEnumerable<int>>([1, 2], CustodyFileFormat.ParseEpochs(Bytes($"1:{Secret(1)}\r\n2:{Secret(2)}{ending}")).Epochs!.Numbers());

    [Theory]
    [InlineData("", "arquivo vazio: ao menos uma época é necessária")]
    [InlineData("\n", "arquivo vazio: ao menos uma época é necessária")]
    [InlineData("1\n", "linha 1: esperado número da época, dois-pontos e o segredo em base64")]
    [InlineData("0:AAAA\n", "linha 1: esperado número da época, dois-pontos e o segredo em base64")]
    [InlineData("01:AAAA\n", "linha 1: esperado número da época, dois-pontos e o segredo em base64")]
    [InlineData("-1:AAAA\n", "linha 1: esperado número da época, dois-pontos e o segredo em base64")]
    [InlineData("1:2:3\n", "linha 1: esperado número da época, dois-pontos e o segredo em base64")]
    [InlineData("1:isto-nao-e-base64!\n", "linha 1: não é base64, 18 bytes")]
    [InlineData("1:AAAA\n", "linha 1: o segredo tem 3 bytes, esperados 32")]
    public void EpochsFileOfAnyOtherFormIsRefusedNamingTheLineAndNeverTheValue(string text, string problem) =>
        Assert.Equal((null, problem), (CustodyFileFormat.ParseEpochs(Bytes(text)).Epochs, CustodyFileFormat.ParseEpochs(Bytes(text)).Problem));

    [Fact]
    public void EpochsOfSeveralLinesWithCarriageReturnsKeepEachSecretOfItsOwn()
    {
        var (epochs, problem) = CustodyFileFormat.ParseEpochs(Bytes($"10:{Secret(10)}\r\n2:{Secret(2)}\r\n3:{Secret(3)}\r\n"));

        Assert.Null(problem);
        Assert.Equal<IEnumerable<int>>([2, 3, 10], epochs!.Numbers());
        Assert.Equal(10, epochs.Newest);
    }

    [Fact]
    public void SecretOfALaterLineThatIsFlawedIsRefusedNamingThatLine()
    {
        var (epochs, problem) = CustodyFileFormat.ParseEpochs(Bytes($"1:{Secret(1)}\n2:{Secret(2)}\n3:AAAA\n"));

        Assert.Equal((null, "linha 3: o segredo tem 3 bytes, esperados 32"), (epochs, problem));
    }

    [Fact]
    public void EpochRepeatedIsRefusedNamingItsNumber()
    {
        var (epochs, problem) = CustodyFileFormat.ParseEpochs(Bytes($"1:{Secret(1)}\n1:{Secret(2)}\n"));

        Assert.Equal((null, "linha 2: época 1 repetida"), (epochs, problem));
    }

    [Fact]
    public void EpochsContentIsZeroedAfterTheReading()
    {
        var content = Bytes($"1:{Secret(1)}\n");

        _ = CustodyFileFormat.ParseEpochs(content);

        Assert.All(content, value => Assert.Equal(0, value));
    }

    private static string Secret(int seed) => Convert.ToBase64String(Enumerable.Range(0, 32).Select(index => (byte)(index + seed)).ToArray());
}
