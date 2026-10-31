using Core.Persistence.ServerGuarantees;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// A reading that fails with a code other than the one for a permission is refused naming what the guarantee itself requires, whichever check ran it.
/// The session the check is given was ended by the server while it waited in the pool, so the failure comes with the first statement and from the
/// server, not from the catalog: nothing the database holds can be told to fail in a way the catalog's qualified names would not already hold out.
/// </summary>
[Trait("Category", "Database")]
public sealed class RequirementNamingTests
{
    [DatabaseFact]
    public async Task ColumnRevokeCheckNamesItsRequirementWhenTheReadingFailsOtherwiseThanOnThePermission()
    {
        var result = await RunWithTheSessionEndedAsync(dataSource => new ColumnRevokeCheck(dataSource));

        AssertRefusedNaming(
            "ColumnRevoke", "use um servidor PostgreSQL que ofereça permissão por coluna (GRANT e REVOKE com lista de colunas)", result);
    }

    [DatabaseFact]
    public async Task ImmutabilityCheckNamesItsRequirementWhenTheReadingFailsOtherwiseThanOnThePermission()
    {
        var result = await RunWithTheSessionEndedAsync(dataSource => new ImmutabilityCheck(dataSource));

        AssertRefusedNaming("Immutability", "o papel da aplicação precisa ler pg_trigger, pg_proc e pg_namespace, presentes em todo PostgreSQL", result);
    }

    [DatabaseFact]
    public async Task PgAuditCheckNamesItsRequirementWhenTheReadingFailsOtherwiseThanOnThePermission()
    {
        var result = await RunWithTheSessionEndedAsync(dataSource => new PgAuditCheck(dataSource));

        AssertRefusedNaming("PgAudit", "o papel da aplicação precisa ler pg_settings e pg_extension", result);
    }

    [DatabaseFact]
    public async Task PrivilegeCheckNamesItsRequirementWhenTheReadingFailsOtherwiseThanOnThePermission()
    {
        var result = await RunWithTheSessionEndedAsync(dataSource => new PrivilegeCheck(dataSource));

        AssertRefusedNaming("Privilege", "o papel da aplicação precisa ler pg_roles e pg_has_role, presentes em todo PostgreSQL", result);
    }

    [DatabaseFact]
    public async Task RlsForceCheckNamesItsRequirementWhenTheReadingFailsOtherwiseThanOnThePermission()
    {
        var result = await RunWithTheSessionEndedAsync(dataSource => new RlsForceCheck(dataSource));

        AssertRefusedNaming("RlsForce", "use um servidor PostgreSQL que ofereça FORCE ROW LEVEL SECURITY", result);
    }

    [DatabaseFact]
    public async Task SerializableCheckNamesItsRequirementWhenTheReadingFailsOtherwiseThanOnThePermission()
    {
        var result = await RunWithTheSessionEndedAsync(dataSource => new SerializableCheck(dataSource));

        AssertRefusedNaming(
            "Serializable",
            "conecte a aplicação a um servidor primário, que aceita transação SERIALIZABLE; réplica em espera não aceita",
            result);
    }

    /// <summary>
    /// The check over a pool that holds one session of the application's role, already ended by the server: the pool gives it back as it is, since it only
    /// learns the session is gone from the first statement sent over it.
    /// </summary>
    private static async Task<CheckResult> RunWithTheSessionEndedAsync(Func<NpgsqlDataSource, IStartupCheck> check)
    {
        await using var database = await ScratchDatabase.CreateAsync();
        var pooled = new NpgsqlConnectionStringBuilder(database.AppConnectionString) { Pooling = true, MaxPoolSize = 1 };
        await using var dataSource = NpgsqlDataSource.Create(pooled.ConnectionString);
        var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        var session = connection.ProcessID;
        await connection.DisposeAsync();
        await EndSessionAsync(session);
        return await check(dataSource).RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Ends the session from the server as the superuser and waits until the server no longer lists it.</summary>
    private static async Task EndSessionAsync(int session)
    {
        var shared = await PostgresFixture.SharedAsync();
        await using var connection = await Sql.OpenAsync(shared.SuperuserConnectionString);
        await using var end = new NpgsqlCommand("SELECT pg_terminate_backend(@session)", connection);
        _ = end.Parameters.AddWithValue("session", session);
        _ = await end.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        await using var listed = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE pid = @session", connection);
        _ = listed.Parameters.AddWithValue("session", session);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while ((long)(await listed.ExecuteScalarAsync(TestContext.Current.CancellationToken))! > 0)
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "The server kept listing the session it was told to end.");
            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }
    }

    private static void AssertRefusedNaming(string name, string requirement, CheckResult result)
    {
        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Equal(name, result.Name);
        Assert.StartsWith("o servidor recusou a conferência", result.Message, StringComparison.Ordinal);
        Assert.EndsWith($": {requirement}", result.Message, StringComparison.Ordinal);
    }
}
