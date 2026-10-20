using Core.Persistence.ServerGuarantees;
using Core.Platform.Startup;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>The application's role passes only when it is neither superuser nor owner; each excess is named with the command that removes it.</summary>
public sealed class PrivilegeCheckTests
{
    [Fact]
    public async Task UnreachableServerIsARefusalNamingTheCheck()
    {
        await using var server = new UnreachableServer();
        var check = new PrivilegeCheck(server.DataSource);

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        RefusalAssert.NamesTheCheckAndTheAddressWithoutTheSecret("Privilege", result, server);
    }

    [Fact]
    public void RoleWithoutAnyExcessPasses()
    {
        var finding = PrivilegeCheck.Judge(Role());

        Assert.Equal(CheckStatus.Passed, finding.Status);
        Assert.Contains("'zqx_app'", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SuperuserFailsWithTheCommandThatRemovesIt()
    {
        var finding = PrivilegeCheck.Judge(Role() with { Superuser = true, Database = "zqx_database" });

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("ALTER ROLE zqx_app NOSUPERUSER", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BypassRlsFailsWithTheCommandThatRemovesIt()
    {
        var finding = PrivilegeCheck.Judge(Role() with { BypassRls = true });

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("ALTER ROLE zqx_app NOBYPASSRLS", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnerOfTheDatabaseFailsNamingTheDatabase()
    {
        var finding = PrivilegeCheck.Judge(Role() with { Database = "zqx_database", Schema = "zqx_schema" });

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("dono do banco zqx_database", finding.Message, StringComparison.Ordinal);
        Assert.Contains("ALTER DATABASE zqx_database OWNER TO", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnerOfASchemaFailsNamingTheSchema()
    {
        var finding = PrivilegeCheck.Judge(Role() with { Schema = "zqx_schema", Table = "zqx_table" });

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("ALTER SCHEMA zqx_schema OWNER TO", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnerOfATableFailsNamingTheTableAndTheMembershipToRevoke()
    {
        var finding = PrivilegeCheck.Judge(Role() with { Table = "zqx_table" });

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("ALTER TABLE zqx_table OWNER TO", finding.Message, StringComparison.Ordinal);
        Assert.Contains("FROM zqx_app", finding.Message, StringComparison.Ordinal);
    }

    private static PrivilegeCheck.RoleReading Role() =>
        new("zqx_app", Superuser: false, BypassRls: false, Database: null, Schema: null, Table: null);
}
