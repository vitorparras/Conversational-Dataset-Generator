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
    [InlineData("AAAA AAAA")]
    [InlineData("AAA")]
    [InlineData("AAAA=AAA")]
    public void KeyTextThatIsNotBase64NamesTheSizeOfTheTextOnly(string text)
    {
        var (key, problem) = CustodyFileFormat.ParseKey(Bytes(text + "\n"));

        Assert.Null(key);
        Assert.Equal($"não é base64, {text.Length} bytes", problem);
    }

    [Fact]
    public void KeyFileThatIsOnlyABreakIsEmpty() =>
        Assert.Equal("arquivo vazio", CustodyFileFormat.ParseKey(Bytes("\r\n")).Problem);

    [Fact]
    public void KeyFileOfTwoLinesCountsTheLines() =>
        Assert.Equal("2 linhas, esperada 1", CustodyFileFormat.ParseKey(Bytes("AAAA\nAAAA\n")).Problem);

    private static byte[] Bytes(string text) => new UTF8Encoding(false).GetBytes(text);
}
