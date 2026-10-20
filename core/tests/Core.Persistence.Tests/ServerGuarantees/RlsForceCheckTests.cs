using Core.Persistence.ServerGuarantees;
using Core.Platform.Startup;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>Every table with a row policy must force it; the first that does not is named with the commands that fix it.</summary>
public sealed class RlsForceCheckTests
{
    [Fact]
    public async Task UnreachableServerIsARefusalNamingTheCheck()
    {
        await using var server = new UnreachableServer();
        var check = new RlsForceCheck(server.DataSource);

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        RefusalAssert.NamesTheCheckAndTheAddressWithoutTheSecret("RlsForce", result, server);
    }

    [Fact]
    public async Task ServerThatNeverAnswersIsARefusalNamingTheAddress()
    {
        await using var server = new SilentServer();
        var check = new RlsForceCheck(server.DataSource);

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Equal("Database:Host", result.ExpectedSource);
        Assert.Equal("o servidor de banco não respondeu: confira o endereço configurado e se o servidor aceita conexões", result.Message);
    }

    [Fact]
    public void EveryPolicyForcedPassesAndCountsTheTables()
    {
        var finding = RlsForceCheck.Judge(forcedTables: 3, unforcedTable: null);

        Assert.Equal(CheckStatus.Passed, finding.Status);
        Assert.StartsWith("3 tabela(s)", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PolicyWithoutForceFailsNamingTheTableAndBothCommands()
    {
        var finding = RlsForceCheck.Judge(forcedTables: 3, unforcedTable: "zqx_table");

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("ALTER TABLE zqx_table ENABLE ROW LEVEL SECURITY", finding.Message, StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE zqx_table FORCE ROW LEVEL SECURITY", finding.Message, StringComparison.Ordinal);
    }
}
