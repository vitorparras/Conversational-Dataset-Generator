using Core.Persistence.ServerGuarantees;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Startup;
using Core.Tests.Integration.Fixtures;
using Npgsql;

namespace Core.Tests.Integration.ServerGuarantees;

/// <summary>
/// The checks against the built image, as the application's role: each passes and records the server's version; where a role
/// without superuser can break the guarantee, the check refuses and names what broke. One class, so that what one test
/// arranges is never seen by another test of the class; nothing here alters app, and no other class counts tables.
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
    public async Task ServerOpensASerializableTransactionForTheApplication()
    {
        var result = await RunAsApplicationAsync(dataSource => new SerializableCheck(dataSource));

        AssertPassedOnTheImage(result);
    }

    [DatabaseFact]
    public async Task ServerLoadsAndInstallsPgaudit()
    {
        var result = await RunAsApplicationAsync(dataSource => new PgAuditCheck(dataSource));

        AssertPassedOnTheImage(result);
        Assert.Contains("pgaudit.log = ", result.Message, StringComparison.Ordinal);
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
        Assert.Contains("ALTER TABLE public.zqx_unforced FORCE ROW LEVEL SECURITY", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task PolicyOnATableWithRowSecurityOffIsRefusedByName()
    {
        var database = await PostgresFixture.SharedAsync();
        await using var table = await OwnedTable.CreateAsync(database.OwnerConnectionString, OwnedTable.Shape.PolicyWithoutRowSecurity);

        var result = await RunAsApplicationAsync(dataSource => new RlsForceCheck(dataSource));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("ALTER TABLE public.zqx_policy_off ENABLE ROW LEVEL SECURITY", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task SuperuserIsRefusedNamingTheAttribute()
    {
        var database = await PostgresFixture.SharedAsync();

        var result = await RunAsync(database.SuperuserConnectionString, dataSource => new PrivilegeCheck(dataSource));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("o papel 'postgres' tem SUPERUSER", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task MemberOfASuperuserRoleIsRefusedNamingTheRoleToRevoke()
    {
        var database = await PostgresFixture.SharedAsync();
        await using var scenario = await ServerScenario.ArrangeAsync(database.SuperuserConnectionString, ServerScenario.Shape.MemberOfASuperuserRole);

        var result = await RunAsync(ServerScenario.RunAs(database.SuperuserConnectionString, ServerScenario.Role), dataSource => new PrivilegeCheck(dataSource));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("execute REVOKE zqx_super FROM zqx_role", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task MemberOfABypassRlsRoleIsRefusedNamingTheRoleToRevoke()
    {
        var database = await PostgresFixture.SharedAsync();
        await using var scenario = await ServerScenario.ArrangeAsync(database.SuperuserConnectionString, ServerScenario.Shape.MemberOfABypassRlsRole);

        var result = await RunAsync(ServerScenario.RunAs(database.SuperuserConnectionString, ServerScenario.Role), dataSource => new PrivilegeCheck(dataSource));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("execute REVOKE zqx_bypass FROM zqx_role", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task MemberOfTheOwnerRoleIsRefusedAsOwnerOfTheDatabase()
    {
        var database = await PostgresFixture.SharedAsync();
        await using var scenario = await ServerScenario.ArrangeAsync(database.SuperuserConnectionString, ServerScenario.Shape.MemberOfTheOwnerRole);

        var result = await RunAsync(ServerScenario.RunAs(database.SuperuserConnectionString, ServerScenario.Role), dataSource => new PrivilegeCheck(dataSource));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("'zqx_role' responde como dono do banco cdg", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task MemberOfATableOwnerIsRefusedNamingTheTable()
    {
        var database = await PostgresFixture.SharedAsync();
        await using var scenario = await ServerScenario.ArrangeAsync(database.SuperuserConnectionString, ServerScenario.Shape.MemberOfATableOwner);

        var result = await RunAsync(ServerScenario.RunAs(database.SuperuserConnectionString, ServerScenario.Role), dataSource => new PrivilegeCheck(dataSource));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Matches(@"'zqx_role' responde como dono da tabela (public\.)?zqx_owned,", result.Message);
    }

    [DatabaseFact]
    public async Task RefusedReadingIsNamedWithoutTheServerText()
    {
        var database = await PostgresFixture.SharedAsync();
        await using var scenario = await ServerScenario.ArrangeAsync(database.SuperuserConnectionString, ServerScenario.Shape.UnreadablePolicyCatalog);

        var result = await RunAsApplicationAsync(dataSource => new RlsForceCheck(dataSource));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Equal(
            "o servidor recusou a conferência (SQLSTATE 42501): o papel da aplicação não pode ler o catálogo do sistema que a "
            + "conferência consulta: devolva a leitura do catálogo a PUBLIC",
            result.Message);
        Assert.StartsWith("PostgreSQL 17.", result.Observed, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task MissingDatabaseIsNamedByItsKey()
    {
        var database = await PostgresFixture.SharedAsync();
        var missing = new NpgsqlConnectionStringBuilder(database.AppConnectionString) { Database = "zqx_missing" };

        var result = await RunAsync(missing.ConnectionString, dataSource => new PrivilegeCheck(dataSource));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Equal("Database:Name", result.ExpectedSource);
        Assert.Contains("SQLSTATE 3D000", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ColumnGrantAloneDoesNotVoidTheColumnRevocation()
    {
        var database = await PostgresFixture.SharedAsync();
        await using var table = await OwnedTable.CreateAsync(database.OwnerConnectionString, OwnedTable.Shape.ColumnGrant);

        var result = await RunAsApplicationAsync(dataSource => new ColumnRevokeCheck(dataSource));

        AssertPassedOnTheImage(result);
        Assert.StartsWith("1 tabela(s)", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task TableWideGrantOverAColumnGrantIsRefusedByName()
    {
        var database = await PostgresFixture.SharedAsync();
        await using var table = await OwnedTable.CreateAsync(database.OwnerConnectionString, OwnedTable.Shape.VoidedColumnGrant);

        var result = await RunAsApplicationAsync(dataSource => new ColumnRevokeCheck(dataSource));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("REVOKE UPDATE ON public.zqx_voided FROM app", result.Message, StringComparison.Ordinal);
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
        return await RunAsync(database.AppConnectionString, check);
    }

    private static async Task<CheckResult> RunAsync(string connectionString, Func<NpgsqlDataSource, IStartupCheck> check)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        return await check(dataSource).RunAsync(TestContext.Current.CancellationToken);
    }

    private static void AssertPassedOnTheImage(CheckResult result)
    {
        Assert.Equal(CheckStatus.Passed, result.Status);
        Assert.Equal("Database:Host", result.ExpectedSource);
        Assert.StartsWith("PostgreSQL 17.", result.Observed, StringComparison.Ordinal);
    }
}
