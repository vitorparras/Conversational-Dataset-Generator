using Core.Persistence.ServerGuarantees;
using Core.Platform.Startup;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>A privilege granted column by column must not be held on the whole table too; the voided grant is named with its revocation.</summary>
public sealed class ColumnRevokeCheckTests
{
    [Fact]
    public async Task UnreachableServerIsARefusalNamingTheCheck()
    {
        await using var server = new UnreachableServer();
        var check = new ColumnRevokeCheck(server.DataSource);

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        RefusalAssert.NamesTheCheckAndTheAddressWithoutTheSecret("ColumnRevoke", result, server);
    }

    [Fact]
    public void ColumnGrantsNotVoidedPassAndCountTheTables()
    {
        var finding = ColumnRevokeCheck.Judge("zqx_app", tablesWithColumnGrants: 2, privilege: null, table: null);

        Assert.Equal(CheckStatus.Passed, finding.Status);
        Assert.StartsWith("2 tabela(s)", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TableWideGrantFailsNamingThePrivilegeTheTableAndTheRevocation()
    {
        var finding = ColumnRevokeCheck.Judge("zqx_app", tablesWithColumnGrants: 2, privilege: "UPDATE", table: "zqx_table");

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("UPDATE na tabela zqx_table inteira", finding.Message, StringComparison.Ordinal);
        Assert.Contains("REVOKE UPDATE ON zqx_table FROM zqx_app", finding.Message, StringComparison.Ordinal);
        Assert.EndsWith("e conceda UPDATE só nas colunas permitidas", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckWithoutADataSourceIsADefect() =>
        _ = Assert.Throws<ArgumentNullException>(() => new ColumnRevokeCheck(null!));
}
