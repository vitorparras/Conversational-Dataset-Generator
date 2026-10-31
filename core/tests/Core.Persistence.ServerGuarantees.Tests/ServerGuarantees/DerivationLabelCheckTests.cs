using Core.Persistence.ServerGuarantees;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Core.Platform.Startup;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>A label already used that the key's set lacks refuses the start and is named, instead of leaving the data under it to look unreadable.</summary>
public sealed class DerivationLabelCheckTests
{
    [Fact]
    public async Task UnreachableServerIsARefusalNamingTheCheck()
    {
        await using var server = new UnreachableServer();
        var check = new DerivationLabelCheck(server.DataSource, LabelsSource.Providing("dominio-a"));

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        RefusalAssert.NamesTheCheckAndTheAddressWithoutTheSecret("DerivationLabels", result, server);
    }

    [Fact]
    public async Task KeyNotReadRefusesWithoutAskingTheServer()
    {
        await using var server = new UnreachableServer();
        var check = new DerivationLabelCheck(server.DataSource, LabelsSource.WithoutKey());

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("a chave-mestra e os rótulos que a acompanham não foram lidos", result.Message, StringComparison.Ordinal);
        Assert.Equal("fonte de teste", result.ExpectedSource);
    }

    [Fact]
    public void LabelsInUseAllProvidedPassAndCountThem()
    {
        var finding = DerivationLabelCheck.Judge(DerivationLabels.Of(["dominio-a", "dominio-b"]), ["dominio-a", "dominio-b"]);

        Assert.Equal(CheckStatus.Passed, finding.Status);
        Assert.StartsWith("2 rótulo(s)", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NoLabelInUseYetPasses()
    {
        var finding = DerivationLabelCheck.Judge(DerivationLabels.None, []);

        Assert.Equal(CheckStatus.Passed, finding.Status);
        Assert.StartsWith("0 rótulo(s)", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LabelsProvidedThatNothingUsesYetPass()
    {
        var finding = DerivationLabelCheck.Judge(DerivationLabels.Of(["dominio-a"]), []);

        Assert.Equal(CheckStatus.Passed, finding.Status);
    }

    [Fact]
    public void LabelInUseThatWasNotProvidedFailsNamingIt()
    {
        var finding = DerivationLabelCheck.Judge(DerivationLabels.Of(["dominio-a"]), ["dominio-a", "dominio-b"]);

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("ausente(s) do conjunto fornecido com a chave: dominio-b;", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SeveralLabelsMissingAreAllNamed()
    {
        var finding = DerivationLabelCheck.Judge(DerivationLabels.None, ["dominio-b", "dominio-a"]);

        Assert.Contains("dominio-a, dominio-b", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingLabelRefusalSaysWhyItMattersAndWhereToGetTheSet()
    {
        var finding = DerivationLabelCheck.Judge(DerivationLabels.None, ["dominio-a"]);

        Assert.Contains("cópia de guarda", finding.Message, StringComparison.Ordinal);
        Assert.Contains("fica ilegível", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckWithoutItsPartsIsADefect() =>
        _ = Assert.Throws<ArgumentNullException>(() => new DerivationLabelCheck(null!, LabelsSource.WithoutKey()));

    [Fact]
    public async Task CheckWithoutASourceIsADefect()
    {
        await using var server = new UnreachableServer();

        _ = Assert.Throws<ArgumentNullException>(() => new DerivationLabelCheck(server.DataSource, null!));
    }
}
