using Core.Persistence.ServerGuarantees;
using Core.Platform.Startup;
using Core.Tests.Integration.Fixtures;
using Npgsql;

namespace Core.Tests.Integration.ServerGuarantees;

/// <summary>
/// The checks against the built image, as the application's role: each passes and records the server's version; where a role
/// without superuser can break the guarantee, the check refuses and names what broke. One class, so that the tables one test
/// creates are never seen by another.
/// </summary>
[Trait("Category", "Database")]
public sealed class ServerGuaranteesTests
{
    [DatabaseFact]
    public async Task ApplicationRoleHoldsNoExcessPrivilege()
    {
        var result = await RunAsApplicationAsync(dataSource => new PrivilegeCheck(dataSource));

        AssertPassedOnTheImage(result);
    }

    [DatabaseFact]
    public async Task OwnerRoleIsRefusedAsOwnerOfTheDatabase()
    {
        var database = await PostgresFixture.SharedAsync();
        await using var dataSource = NpgsqlDataSource.Create(database.OwnerConnectionString);

        var result = await new PrivilegeCheck(dataSource).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("'owner' responde como dono do banco cdg", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task DatabaseWithoutRowPoliciesPassesTheForceCheck()
    {
        var result = await RunAsApplicationAsync(dataSource => new RlsForceCheck(dataSource));

        AssertPassedOnTheImage(result);
    }

    [DatabaseFact]
    public async Task TableWithForcedRowSecurityPasses()
    {
        var database = await PostgresFixture.SharedAsync();
        await using var table = await OwnedTable.CreateAsync(database.OwnerConnectionString, OwnedTable.Shape.ForcedRowSecurity);

        var result = await RunAsApplicationAsync(dataSource => new RlsForceCheck(dataSource));

        AssertPassedOnTheImage(result);
        Assert.StartsWith("1 tabela(s)", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task TableWithRowSecurityWithoutForceIsRefusedByName()
    {
        var database = await PostgresFixture.SharedAsync();
        await using var table = await OwnedTable.CreateAsync(database.OwnerConnectionString, OwnedTable.Shape.UnforcedRowSecurity);

        var result = await RunAsApplicationAsync(dataSource => new RlsForceCheck(dataSource));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("ALTER TABLE zqx_unforced FORCE ROW LEVEL SECURITY", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task PolicyOnATableWithRowSecurityOffIsRefusedByName()
    {
        var database = await PostgresFixture.SharedAsync();
        await using var table = await OwnedTable.CreateAsync(database.OwnerConnectionString, OwnedTable.Shape.PolicyWithoutRowSecurity);

        var result = await RunAsApplicationAsync(dataSource => new RlsForceCheck(dataSource));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("ALTER TABLE zqx_policy_off ENABLE ROW LEVEL SECURITY", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task RefusedCredentialIsNamedWithoutThePassword()
    {
        var database = await PostgresFixture.SharedAsync();
        var wrong = new NpgsqlConnectionStringBuilder(database.AppConnectionString) { Password = "ZQX-SENHA-7731" };
        await using var dataSource = NpgsqlDataSource.Create(wrong.ConnectionString);

        var result = await new PrivilegeCheck(dataSource).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Equal("Database:Username", result.ExpectedSource);
        Assert.Contains("SQLSTATE 28P01", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ZQX-SENHA-7731", result.Message, StringComparison.Ordinal);
    }

    private static async Task<CheckResult> RunAsApplicationAsync(Func<NpgsqlDataSource, IStartupCheck> check)
    {
        var database = await PostgresFixture.SharedAsync();
        await using var dataSource = NpgsqlDataSource.Create(database.AppConnectionString);
        return await check(dataSource).RunAsync(TestContext.Current.CancellationToken);
    }

    private static void AssertPassedOnTheImage(CheckResult result)
    {
        Assert.Equal(CheckStatus.Passed, result.Status);
        Assert.StartsWith("PostgreSQL 17.", result.ExpectedSource, StringComparison.Ordinal);
    }
}
