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
        Assert.StartsWith("a tabela zqx_table tem política de linha que não vale contra o dono: execute ", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckWithoutADataSourceIsADefect() =>
        _ = Assert.Throws<ArgumentNullException>(() => new RlsForceCheck(null!));

    [Fact]
    public void ContentTableWithoutTheDomainColumnFailsNamingTheTable()
    {
        var finding = RlsForceCheck.Judge(forcedTables: 3, unforcedTable: null, new RlsForceCheck.ContentFindings(WithoutDomain: "content.zqx_loose"));

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("content.zqx_loose", finding.Message, StringComparison.Ordinal);
        Assert.Contains("domain_id", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentTableWhoseDomainColumnHasNoForeignKeyFailsNamingTheTable()
    {
        var finding = RlsForceCheck.Judge(forcedTables: 3, unforcedTable: null, new RlsForceCheck.ContentFindings(WithoutDomainKey: "content.zqx_untied"));

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("content.zqx_untied", finding.Message, StringComparison.Ordinal);
        Assert.Contains("chave estrangeira para public.domain", finding.Message, StringComparison.Ordinal);
        Assert.EndsWith("crie as tabelas do esquema content com public.create_content_table", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentTableWithoutAPolicyFailsNamingTheTable()
    {
        var finding = RlsForceCheck.Judge(forcedTables: 3, unforcedTable: null, new RlsForceCheck.ContentFindings(WithoutPolicy: "content.zqx_open"));

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("content.zqx_open", finding.Message, StringComparison.Ordinal);
        Assert.Contains("política de linha", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentTableWithAPermissivePolicyBeyondTheDomainOneFailsNamingTheTable()
    {
        var finding = RlsForceCheck.Judge(forcedTables: 3, unforcedTable: null, new RlsForceCheck.ContentFindings(OpenPolicy: "content.zqx_wide"));

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("content.zqx_wide", finding.Message, StringComparison.Ordinal);
        Assert.Contains("DROP POLICY", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewInTheContentSchemaFailsNamingIt()
    {
        var finding = RlsForceCheck.Judge(forcedTables: 3, unforcedTable: null, new RlsForceCheck.ContentFindings(NotATable: "content.zqx_snap"));

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("content.zqx_snap", finding.Message, StringComparison.Ordinal);
        Assert.Contains("não é tabela", finding.Message, StringComparison.Ordinal);
        Assert.EndsWith("o esquema content só guarda tabelas criadas com public.create_content_table", finding.Message, StringComparison.Ordinal);
    }
}
