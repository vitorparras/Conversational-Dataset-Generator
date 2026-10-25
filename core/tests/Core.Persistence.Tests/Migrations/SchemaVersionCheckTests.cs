using System.Text;
using Core.Persistence.Migrations;
using Core.Persistence.Tests.ServerGuarantees;
using Core.Platform.Startup;

namespace Core.Persistence.Tests.Migrations;

/// <summary>The startup check only reads the version and refuses what is behind, ahead or diverging, naming what it found and what it expects.</summary>
public sealed class SchemaVersionCheckTests
{
    private static readonly MigrationStep First = MigrationStep.Of(1, "0001-first", Encoding.UTF8.GetBytes("a"));
    private static readonly MigrationStep Second = MigrationStep.Of(2, "0002-second", Encoding.UTF8.GetBytes("b"));

    [Fact]
    public async Task UnreachableServerIsARefusalNamingTheCheck()
    {
        await using var server = new UnreachableServer();
        var check = new SchemaVersionCheck(server.DataSource, [First]);

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        RefusalAssert.NamesTheCheckAndTheAddressWithoutTheSecret("SchemaVersion", result, server);
    }

    [Fact]
    public void UpToDatePassesShowingTheVersion()
    {
        var finding = SchemaVersionCheck.Judge(SchemaComparison.Compare([First, Second], [Applied(First), Applied(Second)]));

        Assert.Equal(CheckStatus.Passed, finding.Status);
        Assert.Contains("versão 2", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyRegistryFailsAtVersionZeroSayingToRunTheMigrationMode()
    {
        var finding = SchemaVersionCheck.Judge(SchemaComparison.Compare([First, Second], []));

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("versão 0", finding.Message, StringComparison.Ordinal);
        Assert.Contains("espera a 2", finding.Message, StringComparison.Ordinal);
        Assert.Contains("modo migrate", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AheadFailsNamingTheVersionFoundAndTheOneExpected()
    {
        var finding = SchemaVersionCheck.Judge(SchemaComparison.Compare([First], [Applied(First), Applied(Second)]));

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("versão 2", finding.Message, StringComparison.Ordinal);
        Assert.Contains("da 1", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DivergentFailsNamingTheStepAndBothDigestsAndDoesNotAdviseMigrating()
    {
        var changed = MigrationStep.Of(1, "0001-first", Encoding.UTF8.GetBytes("changed"));

        var finding = SchemaVersionCheck.Judge(SchemaComparison.Compare([changed], [Applied(First)]));

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("0001-first", finding.Message, StringComparison.Ordinal);
        Assert.Contains(First.Sha256, finding.Message, StringComparison.Ordinal);
        Assert.Contains(changed.Sha256, finding.Message, StringComparison.Ordinal);
        Assert.Contains("não rode o modo migrate", finding.Message, StringComparison.Ordinal);
    }

    private static AppliedStep Applied(MigrationStep step) => new(step.Version, step.Name, step.Sha256);
}
