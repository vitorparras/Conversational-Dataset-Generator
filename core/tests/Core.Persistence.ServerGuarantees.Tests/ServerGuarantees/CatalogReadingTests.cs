using Core.Persistence.Migrations;
using Core.Persistence.ServerGuarantees;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// Each check run against a real server, as the application's role: it reads the columns it names from the catalog, and a refusal says what the server
/// answered. The judges are tested apart, with the readings made by hand, so only a run against the server can tell that the reading itself is right.
/// </summary>
[Trait("Category", "Database")]
public sealed class CatalogReadingTests
{
    private const string CatalogDenied =
        "o servidor recusou a conferência (SQLSTATE 42501): o papel da aplicação não pode ler o catálogo do sistema que a conferência consulta: devolva a leitura do catálogo a PUBLIC";

    [DatabaseFact]
    public async Task PrivilegeCheckReadsTheRoleAndTheOwnershipsItJudges()
    {
        var result = await RunAsApplicationAsync(dataSource => new PrivilegeCheck(dataSource));

        AssertPassedOn("Privilege", result);
        Assert.Contains("o papel 'app' não é superusuário", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task RlsForceCheckReadsEveryColumnOfItsQuery()
    {
        var result = await RunAsApplicationAsync(dataSource => new RlsForceCheck(dataSource));

        AssertPassedOn("RlsForce", result);
        Assert.StartsWith("0 tabela(s)", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task RlsForceCheckReadsTheContentSchemaOfAMigratedDatabase()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await database.RunAsOwnerAsync("SELECT public.create_content_table('zqx_item', 'body text NOT NULL')");

        var result = await AppStartup.CheckAsync(database, dataSource => new RlsForceCheck(dataSource));

        AssertPassedOn("RlsForce", result);
        Assert.StartsWith("1 tabela(s)", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ColumnRevokeCheckReadsTheTablesWithColumnGrantsAndTheirPrivilege()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        await database.RunAsOwnerAsync("CREATE TABLE public.zqx_by_column (a integer)", "GRANT UPDATE (a) ON public.zqx_by_column TO app");

        var result = await AppStartup.CheckAsync(database, dataSource => new ColumnRevokeCheck(dataSource));

        AssertPassedOn("ColumnRevoke", result);
        Assert.StartsWith("1 tabela(s)", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ColumnRevokeCheckNamesTheRoleTheTableAndThePrivilegeItReadFromTheServer()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        await database.RunAsOwnerAsync(
            "CREATE TABLE public.zqx_voided (a integer)",
            "GRANT UPDATE (a) ON public.zqx_voided TO app",
            "GRANT UPDATE ON public.zqx_voided TO app");

        var result = await AppStartup.CheckAsync(database, dataSource => new ColumnRevokeCheck(dataSource));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("o papel 'app' tem UPDATE na tabela public.zqx_voided inteira", result.Message, StringComparison.Ordinal);
        Assert.Contains("REVOKE UPDATE ON public.zqx_voided FROM app", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ImmutabilityCheckReadsTheTriggersOfAMigratedDatabaseAndAccusesTheOneDisabled()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());

        var held = await AppStartup.CheckAsync(database, dataSource => new ImmutabilityCheck(dataSource));
        await database.RunAsOwnerAsync("ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_emptied");
        var disabled = await AppStartup.CheckAsync(database, dataSource => new ImmutabilityCheck(dataSource));

        AssertPassedOn("Immutability", held);
        Assert.Equal(CheckStatus.Failed, disabled.Status);
        Assert.Contains("custody_event_never_emptied de public.custody_event está desabilitado", disabled.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("custody_event_never_changed", disabled.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task PgAuditCheckReadsTheSettingAndTheVersionOfTheInstalledExtension()
    {
        var result = await RunAsApplicationAsync(dataSource => new PgAuditCheck(dataSource));

        AssertPassedOn("PgAudit", result);
        Assert.Matches(@"^pgaudit \d+(\.\d+)* carregado e instalado; pgaudit\.log = '[^']+'$", result.Message);
    }

    [DatabaseFact]
    public async Task SerializableCheckOpensTheTransactionOnTheServer()
    {
        var result = await RunAsApplicationAsync(dataSource => new SerializableCheck(dataSource));

        AssertPassedOn("Serializable", result);
        Assert.Equal("o servidor abre transação SERIALIZABLE para o papel da aplicação", result.Message);
    }

    [DatabaseFact]
    public async Task CatalogTheRoleCannotReadIsRefusedWithoutTheServerText()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        await RevokePolicyCatalogReadAsync(database);

        var result = await AppStartup.CheckAsync(database, dataSource => new RlsForceCheck(dataSource));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Equal("RlsForce", result.Name);
        Assert.Equal(CatalogDenied, result.Message);
        Assert.StartsWith("PostgreSQL ", result.Observed, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task TableOfItsOwnTheRoleCannotReadIsRefusedWithTheRequirementAndNotAsTheCatalog()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await database.RunAsOwnerAsync("REVOKE SELECT ON public.derivation_label FROM app, PUBLIC");

        var result = await AppStartup.CheckAsync(database, dataSource => new DerivationLabelCheck(dataSource, LabelsSource.Providing("zqx-domain-a")));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.StartsWith("o servidor recusou a conferência (SQLSTATE 42501): rode o modo migrate", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task OtherRefusalOfTheServerWhileReadingTheCatalogIsNamedByTheRequirementAndNotAsTheCatalog()
    {
        var shared = await PostgresFixture.SharedAsync();
        await using var dataSource = NpgsqlDataSource.Create(shared.AppConnectionString);
        var guarantee = new Guarantee("Zqx", "a exigência do teste", (connection, token) =>
            ServerProbe.ReadRowAsync(connection, "SELECT 1 / 0", row => Finding.Holds("nunca"), token));

        var result = await ServerProbe.RunAsync(dataSource, guarantee, TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Equal("o servidor recusou a conferência (SQLSTATE 22012): a exigência do teste", result.Message);
    }

    [DatabaseFact]
    public async Task FailureOfTheConnectionWhileReadingIsNamedByTheRequirementWithoutAnSqlState()
    {
        var shared = await PostgresFixture.SharedAsync();
        await using var dataSource = NpgsqlDataSource.Create(shared.AppConnectionString);
        var guarantee = new Guarantee("Zqx", "a exigência do teste", (_, _) => throw new NpgsqlException("a conexão caiu"));

        var result = await ServerProbe.RunAsync(dataSource, guarantee, TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Equal("o servidor recusou a conferência: a exigência do teste", result.Message);
        Assert.StartsWith("PostgreSQL ", result.Observed, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task MissingDatabaseIsRefusedNamingTheStateAndTheKeyOfTheName()
    {
        var shared = await PostgresFixture.SharedAsync();
        var missing = new NpgsqlConnectionStringBuilder(shared.AppConnectionString) { Database = "zqx_missing" };
        await using var dataSource = NpgsqlDataSource.Create(missing.ConnectionString);

        var result = await new PrivilegeCheck(dataSource).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Equal("Database:Name", result.ExpectedSource);
        Assert.Equal("o servidor recusou a conexão da aplicação (SQLSTATE 3D000): confira o banco, o papel e a senha configurados", result.Message);
    }

    [DatabaseFact]
    public async Task RefusedPasswordIsRefusedNamingTheStateAndTheKeyOfTheUsername()
    {
        var shared = await PostgresFixture.SharedAsync();
        var wrong = new NpgsqlConnectionStringBuilder(shared.AppConnectionString) { Password = "ZQX-SENHA-7731" };
        await using var dataSource = NpgsqlDataSource.Create(wrong.ConnectionString);

        var result = await new PrivilegeCheck(dataSource).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Equal("Database:Username", result.ExpectedSource);
        Assert.Equal("o servidor recusou a conexão da aplicação (SQLSTATE 28P01): confira o banco, o papel e a senha configurados", result.Message);
    }

    /// <summary>The check as the application's role, in a database of its own, so that no table or grant of another test is read.</summary>
    private static async Task<CheckResult> RunAsApplicationAsync(Func<NpgsqlDataSource, IStartupCheck> check)
    {
        await using var database = await ScratchDatabase.CreateAsync();
        return await AppStartup.CheckAsync(database, check);
    }

    /// <summary>Takes the read of pg_policy from PUBLIC as the superuser, inside the scratch database, whose own catalog alone is changed.</summary>
    private static async Task RevokePolicyCatalogReadAsync(ScratchDatabase database)
    {
        var shared = await PostgresFixture.SharedAsync();
        var inScratch = new NpgsqlConnectionStringBuilder(shared.SuperuserConnectionString)
        {
            Database = new NpgsqlConnectionStringBuilder(database.OwnerConnectionString).Database,
            Pooling = false,
        };
        await using var connection = await Sql.OpenAsync(inScratch.ConnectionString);
        await using var command = new NpgsqlCommand("REVOKE SELECT ON pg_catalog.pg_policy FROM PUBLIC", connection);
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static void AssertPassedOn(string name, CheckResult result)
    {
        Assert.Equal(CheckStatus.Passed, result.Status);
        Assert.Equal(name, result.Name);
        Assert.Equal("Database:Host", result.ExpectedSource);
        Assert.StartsWith("PostgreSQL ", result.Observed, StringComparison.Ordinal);
    }
}
