using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.People;

/// <summary>
/// One database of its own, migrated to the last version, for all the tests of a class: a test names its people with a name
/// nobody else uses and counts the rows before and after when it asserts that nothing was written. The database is made on
/// first use, so a class whose tests are skipped for want of Docker never asks for one.
/// </summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class PeopleDatabase : IAsyncDisposable
{
    private ScratchDatabase? database;
    private NpgsqlDataSource? application;

    /// <summary>The registry, writing as the application's role.</summary>
    internal async Task<PersonRegistry> RegistryAsync()
    {
        var scratch = await ScratchAsync();
        application ??= NpgsqlDataSource.Create(scratch.AppConnectionString);
        return new PersonRegistry(application);
    }

    /// <summary>The store of passwords, writing as the application's role and deriving with the work factor given.</summary>
    internal async Task<PersonCredentials> CredentialsAsync(int workFactor)
    {
        var scratch = await ScratchAsync();
        application ??= NpgsqlDataSource.Create(scratch.AppConnectionString);
        return new PersonCredentials(application, new PasswordDerivation(workFactor));
    }

    /// <summary>The store of passwords over a data source of its own, whose driver logs every line to the log that comes with it.</summary>
    internal async Task<LoggedCredentials> LoggedCredentialsAsync(int workFactor) =>
        new((await ScratchAsync()).AppConnectionString, workFactor);

    /// <summary>The credential as the table holds it, or null when the person has none.</summary>
    internal async Task<CredentialRow?> CredentialOfAsync(long personId)
    {
        await using var connection = await Sql.OpenAsync((await ScratchAsync()).OwnerConnectionString);
        await using var command = new NpgsqlCommand("SELECT algorithm, work_factor, salt, derived FROM public.person_credential WHERE person_id = @id", connection);
        _ = command.Parameters.AddWithValue("id", personId);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        if (!await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            return null;
        }

        var salt = await reader.GetFieldValueAsync<byte[]>(2, TestContext.Current.CancellationToken);
        var derived = await reader.GetFieldValueAsync<byte[]>(3, TestContext.Current.CancellationToken);
        return new CredentialRow(reader.GetString(0), reader.GetInt32(1), salt, derived);
    }

    /// <summary>A name of use no other test has.</summary>
    internal static string NewName() => $"zqx-pessoa-{Guid.NewGuid():N}";

    /// <summary>The identifier of the person the operation wrote or changed.</summary>
    internal static long Id(PersonResult result)
    {
        Assert.Equal(PersonOutcome.Done, result.Outcome);
        return result.PersonId!.Value;
    }

    /// <summary>The rows of the table, as the role owner.</summary>
    [SuppressMessage("Security", "CA2100", Justification = "Each statement is a literal chosen by the table name, never built from it.")]
    internal async Task<long> CountAsync(string table)
    {
        var statement = table switch
        {
            "person" => "SELECT count(*) FROM public.person",
            "person_change" => "SELECT count(*) FROM public.person_change",
            _ => throw new ArgumentOutOfRangeException(nameof(table), table, "Only the tables of the registry of people are counted."),
        };
        await using var connection = await Sql.OpenAsync((await ScratchAsync()).OwnerConnectionString);
        await using var command = new NpgsqlCommand(statement, connection);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    /// <summary>The person as the table holds it, or null when no row has the identifier.</summary>
    internal async Task<PersonRow?> PersonAsync(long personId)
    {
        await using var connection = await Sql.OpenAsync((await ScratchAsync()).OwnerConnectionString);
        await using var command = new NpgsqlCommand("SELECT name, function_name, active, created_at, created_by FROM public.person WHERE id = @id", connection);
        _ = command.Parameters.AddWithValue("id", personId);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        if (!await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            return null;
        }

        var createdAt = await reader.GetFieldValueAsync<DateTimeOffset>(3, TestContext.Current.CancellationToken);
        return new PersonRow(reader.GetString(0), reader.GetString(1), reader.GetBoolean(2), createdAt, reader.GetString(4));
    }

    /// <summary>What was done to the person, oldest first.</summary>
    internal async Task<IReadOnlyList<ChangeRow>> ChangesOfAsync(long personId)
    {
        await using var connection = await Sql.OpenAsync((await ScratchAsync()).OwnerConnectionString);
        await using var command = new NpgsqlCommand(
            "SELECT kind, previous_function, new_function, actor_id, changed_at, changed_by FROM public.person_change WHERE person_id = @id ORDER BY id", connection);
        _ = command.Parameters.AddWithValue("id", personId);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var rows = new List<ChangeRow>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            var changedAt = await reader.GetFieldValueAsync<DateTimeOffset>(4, TestContext.Current.CancellationToken);
            rows.Add(new ChangeRow(
                reader.GetString(0),
                await reader.IsDBNullAsync(1, TestContext.Current.CancellationToken) ? null : reader.GetString(1),
                await reader.IsDBNullAsync(2, TestContext.Current.CancellationToken) ? null : reader.GetString(2),
                await reader.IsDBNullAsync(3, TestContext.Current.CancellationToken) ? null : reader.GetInt64(3),
                changedAt,
                reader.GetString(5)));
        }

        return rows;
    }

    /// <summary>What the server answers to a statement run as the application's role.</summary>
    internal async Task<PostgresException> RefusedToAppAsync(string statement) =>
        await RefusedAsync((await ScratchAsync()).AppConnectionString, statement);

    /// <summary>What the server answers to a statement run as the role that owns the database.</summary>
    internal async Task<PostgresException> RefusedToOwnerAsync(string statement) =>
        await RefusedAsync((await ScratchAsync()).OwnerConnectionString, statement);

    /// <summary>A role that can connect and use the public schema and holds no grant on any table, dropped on dispose.</summary>
    internal async Task<ScratchRole> BareRoleAsync() => await (await ScratchAsync()).CreateBareRoleAsync();

    /// <summary>What the server answers to a statement run over the connection string given.</summary>
    internal static Task<PostgresException> RefusedOverAsync(string connectionString, string statement) => RefusedAsync(connectionString, statement);

    /// <summary>The first column of the rows the query returns, as the role owner, in the order the query gives.</summary>
    [SuppressMessage("Security", "CA2100", Justification = "The queries are literals of the calling tests, never input.")]
    internal async Task<IReadOnlyList<string>> OwnerColumnAsync(string query)
    {
        await using var connection = await Sql.OpenAsync((await ScratchAsync()).OwnerConnectionString);
        await using var command = new NpgsqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var values = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    public async ValueTask DisposeAsync()
    {
        if (application is not null)
        {
            await application.DisposeAsync();
        }

        if (database is not null)
        {
            await database.DisposeAsync();
        }
    }

    [SuppressMessage("Security", "CA2100", Justification = "The statement is a literal of the calling test, never input.")]
    private static async Task<PostgresException> RefusedAsync(string connectionString, string statement)
    {
        await using var connection = await Sql.OpenAsync(connectionString);
        await using var command = new NpgsqlCommand(statement, connection);
        return await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
    }

    private async Task<ScratchDatabase> ScratchAsync()
    {
        if (database is null)
        {
            var made = await ScratchDatabase.CreateAsync();
            _ = await made.MigrateAsync(EmbeddedMigrations.Load());
            database = made;
        }

        return database;
    }

    /// <summary>One row of <c>public.person</c>, without its identifier.</summary>
    internal sealed record PersonRow(string Name, string Function, bool Active, DateTimeOffset CreatedAt, string CreatedBy);

    /// <summary>One row of <c>public.person_credential</c>, without its identifier.</summary>
    internal sealed record CredentialRow(string Algorithm, int WorkFactor, byte[] Salt, byte[] Derived);

    /// <summary>One row of <c>public.person_change</c>, without its identifiers.</summary>
    internal sealed record ChangeRow(string Kind, string? PreviousFunction, string? NewFunction, long? ActorId, DateTimeOffset ChangedAt, string ChangedBy);
}
