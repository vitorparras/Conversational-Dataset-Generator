using Core.Persistence.Settings;
using Core.Persistence.Tests.Fixtures;
using Core.Persistence.Tests.People;
using Npgsql;

namespace Core.Persistence.Tests.Settings;

/// <summary>
/// The credential of the applier of the configuration (#634, part 4; decision 0053, CA-7): the applier's account writes nothing in the database. The check finds a credential that writes, as the
/// application's does; does not find one that only reads, which still reads what the applier needs and is refused by the server when it tries to write.
/// </summary>
[Trait("Category", "Database")]
public sealed class CredentialPrivilegesTests : IClassFixture<PeopleDatabase>
{
    private readonly PeopleDatabase database;

    public CredentialPrivilegesTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task Ca7TheCredentialOfTheApplicationWritesAndIsFound() =>
        Assert.True(await CredentialPrivileges.CanWriteAsync(await database.ApplicationAsync(), TestContext.Current.CancellationToken));

    [DatabaseFact]
    public async Task Ca7ARoleThatOnlyReadsIsNotFoundWritingStillReadsTheApprovedChangesAndIsRefusedAWrite()
    {
        var scratch = await database.ScratchAsync();
        await using var role = await scratch.CreateBareRoleAsync();
        await GrantReadAsync(scratch.OwnerConnectionString, role.Name);
        await using var source = NpgsqlDataSource.Create(role.ConnectionString);

        var writes = await CredentialPrivileges.CanWriteAsync(source, TestContext.Current.CancellationToken);
        var pending = await new ApprovedChanges(source).PendingAsync(TestContext.Current.CancellationToken);
        var refused = await Assert.ThrowsAsync<PostgresException>(() => WriteAsync(source));

        Assert.False(writes);
        Assert.NotNull(pending);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    [DatabaseFact]
    public async Task Ca7ARoleThatWritesOnlyInTheContentSchemaIsFound()
    {
        var scratch = await database.ScratchAsync();
        await using var role = await scratch.CreateBareRoleAsync();
        await GrantReadAsync(scratch.OwnerConnectionString, role.Name);
        await GrantWriteInContentAsync(scratch.OwnerConnectionString, role.Name);
        await using var source = NpgsqlDataSource.Create(role.ConnectionString);

        Assert.True(await CredentialPrivileges.CanWriteAsync(source, TestContext.Current.CancellationToken));
    }

    private static async Task GrantReadAsync(string ownerConnectionString, string role)
    {
        await using var connection = await Sql.OpenAsync(ownerConnectionString);
        // nosemgrep: sql-concatenation-csharp interpolates the name of a role the fixture made itself, never input.
        await using var command = new NpgsqlCommand($"GRANT SELECT ON ALL TABLES IN SCHEMA public TO {role}", connection);
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task GrantWriteInContentAsync(string ownerConnectionString, string role)
    {
        await using var connection = await Sql.OpenAsync(ownerConnectionString);
        // nosemgrep: sql-concatenation-csharp interpolates the name of a role the fixture made itself, never input.
        await using var command = new NpgsqlCommand(
            $"""
            CREATE TABLE IF NOT EXISTS content.zqx_credential_6340 (id integer);
            GRANT USAGE ON SCHEMA content TO {role};
            GRANT INSERT ON content.zqx_credential_6340 TO {role}
            """,
            connection);
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task WriteAsync(NpgsqlDataSource source)
    {
        var command = source.CreateCommand("UPDATE public.configuration_change SET motive = motive");
        await using (command.ConfigureAwait(false))
        {
            _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }
}
