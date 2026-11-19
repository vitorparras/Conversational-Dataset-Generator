using Core.Ingestion.Sanitizing;
using static Core.Ingestion.Tests.Sanitizing.Sanitizing;

namespace Core.Ingestion.Tests.Sanitizing;

/// <summary>What the form of origin repeated on every page leaves, and nothing written goes with it; in doubt the artifact stays (issue 89).</summary>
public sealed class FormatArtifactStepTests
{
    private const string Header = "Relatório ZQX-7 — cópia";

    private static SanitizedText Treated(string raw) => Sanitized(Sanitizer(new FormatArtifactStep(Boundaries)), raw);

    [Fact]
    public void HeaderRepeatedOnEveryPageLeavesEveryPageAndNoOtherLine()
    {
        const string raw = Header + "\nLinha um da página.\nOutra linha.\f" + Header + "\nLinha dois.\f\n" + Header + "\nLinha três.\nFim.";

        var treated = Treated(raw);

        Assert.Equal("Linha um da página.\nOutra linha.\fLinha dois.\f\nLinha três.\nFim.", treated.Text);
        Assert.Equal(("artefatos", 3, 0), (treated.Report[0].Name, treated.Report[0].Count, treated.Report[0].Untreated));
    }

    [Fact]
    public void WrittenTextMatchingAnArtifactStaysAndIsCounted()
    {
        const string raw = Header + "\nO cabeçalho dizia:\n" + Header + "\nE seguiu.\f" + Header + "\nLinha dois.\f" + Header + "\nLinha três.";

        var treated = Treated(raw);

        Assert.Equal("O cabeçalho dizia:\n" + Header + "\nE seguiu.\fLinha dois.\fLinha três.", treated.Text);
        Assert.Equal((3, 1), (treated.Report[0].Count, treated.Report[0].Untreated));
    }

    [Theory]
    [InlineData("[10:02] zqx1: oi\n[10:03] zqx2: tudo bem?\n[10:03] zqx1: oi")]
    [InlineData("Primeira página.\nTexto um.\fSegunda página.\nTexto dois.\fTerceira página.\nTexto três.")]
    [InlineData("Igual\nTexto um.\fIgual\nTexto dois.")]
    public void FormWithoutArtifactsLeavesUnchanged(string raw)
    {
        var treated = Treated(raw);

        Assert.Equal((raw, 0), (treated.Text, treated.Report[0].Count));
    }

    [Fact]
    public void PlaceholderForMissingContentStays()
    {
        const string raw = Header + "\nEsta mensagem foi apagada\nzqx1: texto a\n<Mídia oculta>\n<ZQX rodapé\f"
            + Header + "\nEsta mensagem foi apagada\nzqx2: texto b\n<Mídia oculta>\n<ZQX rodapé\f"
            + Header + "\nEsta mensagem foi apagada\nzqx1: texto c\n<Mídia oculta>\n<ZQX rodapé";

        var treated = Treated(raw);

        Assert.Equal(
            "Esta mensagem foi apagada\nzqx1: texto a\n<Mídia oculta>\n\fEsta mensagem foi apagada\nzqx2: texto b\n<Mídia oculta>\n\f"
                + "Esta mensagem foi apagada\nzqx1: texto c\n<Mídia oculta>\n",
            treated.Text);
    }

    [Fact]
    public void ArtifactRemovalTwiceEqualsOnce()
    {
        const string raw = Header + "\nSeção ZQX\nTexto um.\nPágina 1 de 3\f" + Header + "\nSeção ZQX\nTexto dois.\nPágina 2 de 3\f"
            + Header + "\nSeção ZQX\nTexto três.\nPágina 3 de 3";

        var once = Treated(raw).Text;
        var twice = Treated(once).Text;

        Assert.Equal("Texto um.\n\fTexto dois.\n\fTexto três.\n", once);
        Assert.Equal(once, twice);
    }
}
