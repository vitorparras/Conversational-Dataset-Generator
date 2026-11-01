using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Custody;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// One database of its own, migrated to the last version, for all the tests of a class; the chain in it is the one these tests write, and the first event of a class is the first of its chain.
/// People are made as the role owner, so that the chain holds only what the test itself appended. The database is made on first use.
/// </summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class CustodyDatabase : IAsyncDisposable
{
    private ScratchDatabase? database;
    private NpgsqlDataSource? application;

    /// <summary>The chain, writing as the application's role.</summary>
    internal async Task<CustodyChain> ChainAsync() => new(await DataSourceAsync());

    /// <summary>The data source built with the application's credential.</summary>
    internal async Task<NpgsqlDataSource> DataSourceAsync()
    {
        var scratch = await ScratchAsync();
        application ??= NpgsqlDataSource.Create(scratch.AppConnectionString);
        return application;
    }

    /// <summary>The registry of people, writing as the application's role.</summary>
    internal async Task<PersonRegistry> RegistryAsync()
    {
        _ = await ChainAsync();
        return new PersonRegistry(application!);
    }

    /// <summary>The store of passwords, writing as the application's role with the work factor given.</summary>
    internal async Task<PersonCredentials> CredentialsAsync(int workFactor)
    {
        _ = await ChainAsync();
        return new PersonCredentials(application!, new PasswordDerivation(workFactor));
    }

    /// <summary>A person made as the role owner, active or deactivated; the identifier is all the chain needs of them.</summary>
    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of this method, never input.")]
    internal async Task<long> PersonAsync(bool active)
    {
        await using var connection = await Sql.OpenAsync((await ScratchAsync()).OwnerConnectionString);
        await using var command = new NpgsqlCommand("INSERT INTO public.person (name, function_name, active) VALUES (@name, 'Anotação', @active) RETURNING id", connection);
        _ = command.Parameters.AddWithValue("name", $"zqx-pessoa-{Guid.NewGuid():N}");
        _ = command.Parameters.AddWithValue("active", active);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    /// <summary>A writer of the application's role that has added an event in a transaction of the isolation given and holds it open, so that whoever reaches the same link waits for it.</summary>
    internal async Task<HeldAppend> HoldAsync(long item, IsolationLevel isolation) =>
        await HeldAppend.BeginAsync((await ScratchAsync()).AppConnectionString, item, isolation);

    /// <summary>Waits until a writer of this database waits for a lock, which is what a writer does when it meets one that holds the link open; false when none came to wait.</summary>
    internal async Task<bool> WaitForABlockedWriterAsync()
    {
        var name = new NpgsqlConnectionStringBuilder((await ScratchAsync()).AppConnectionString).Database;
        await using var connection = await Sql.OpenAsync((await PostgresFixture.SharedAsync()).SuperuserConnectionString);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname = @name AND wait_event_type = 'Lock'", connection);
        _ = command.Parameters.AddWithValue("name", name!);
        var waited = Stopwatch.StartNew();
        while (waited.Elapsed < TimeSpan.FromSeconds(30))
        {
            if ((long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))! > 0)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);
        }

        return false;
    }

    /// <summary>Whether the query, run as the role owner, answers true.</summary>
    internal async Task<bool> FlagAsOwnerAsync(string query, params (string Name, object Value)[] parameters) =>
        (bool)(await ScalarAsOwnerAsync(query, parameters))!;

    /// <summary>The number the query, run as the role owner, counts.</summary>
    internal async Task<long> CountAsOwnerAsync(string query, params (string Name, object Value)[] parameters) =>
        (long)(await ScalarAsOwnerAsync(query, parameters))!;

    /// <summary>The text the query, run as the role owner, answers.</summary>
    internal async Task<string> TextAsOwnerAsync(string query, params (string Name, object Value)[] parameters) =>
        (string)(await ScalarAsOwnerAsync(query, parameters))!;

    [SuppressMessage("Security", "CA2100", Justification = "The queries are literals of the calling tests, never input.")]
    private async Task<object?> ScalarAsOwnerAsync(string query, (string Name, object Value)[] parameters)
    {
        await using var connection = await Sql.OpenAsync((await ScratchAsync()).OwnerConnectionString);
        await using var command = new NpgsqlCommand(query, connection);
        Array.ForEach(parameters, parameter => command.Parameters.AddWithValue(parameter.Name, parameter.Value));
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Every event of the chain, as the table holds it, in the order of the sequence.</summary>
    internal async Task<IReadOnlyList<EventRow>> EventsAsync()
    {
        await using var connection = await Sql.OpenAsync((await ScratchAsync()).OwnerConnectionString);
        await using var command = new NpgsqlCommand(
            "SELECT sequence, occurred_at, actor_person_id, actor_system, item_kind, item_id, step, reason, corrects, previous_digest, digest, recorded_by FROM public.custody_event ORDER BY sequence",
            connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var rows = new List<EventRow>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add(await EventRow.ReadAsync(reader));
        }

        return rows;
    }

    /// <summary>Runs the statement as the role that owns the database.</summary>
    internal async Task RunAsOwnerAsync(string statement) => await (await ScratchAsync()).RunAsOwnerAsync(statement);

    /// <summary>What the server answers to a statement run as the application's role.</summary>
    internal async Task<PostgresException> RefusedToAppAsync(string statement) =>
        await RefusedAsync((await ScratchAsync()).AppConnectionString, statement);

    /// <summary>What the server answers to a statement run as the role that owns the database.</summary>
    internal async Task<PostgresException> RefusedToOwnerAsync(string statement) =>
        await RefusedAsync((await ScratchAsync()).OwnerConnectionString, statement);

    /// <summary>What the server answers to a statement run on a connection the caller built, to this database or not.</summary>
    internal static async Task<PostgresException> RefusedOnAsync(string connectionString, string statement) =>
        await RefusedAsync(connectionString, statement);

    /// <summary>The connection string of the application's role to this database.</summary>
    internal async Task<string> AppConnectionStringAsync() => (await ScratchAsync()).AppConnectionString;

    /// <summary>The connection string of the role that owns this database.</summary>
    internal async Task<string> OwnerConnectionStringAsync() => (await ScratchAsync()).OwnerConnectionString;

    /// <summary>The connection string of the server's superuser to this database, for what neither the application nor the owner may arrange.</summary>
    internal async Task<string> SuperuserConnectionStringAsync()
    {
        var name = new NpgsqlConnectionStringBuilder((await ScratchAsync()).AppConnectionString).Database;
        return new NpgsqlConnectionStringBuilder((await PostgresFixture.SharedAsync()).SuperuserConnectionString) { Database = name, Pooling = false }.ConnectionString;
    }

    /// <summary>Runs the statement as the server's superuser, connected to this database.</summary>
    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of the calling tests, never input.")]
    internal async Task RunAsSuperuserAsync(string statement)
    {
        await using var connection = await Sql.OpenAsync(await SuperuserConnectionStringAsync());
        await using var command = new NpgsqlCommand(statement, connection);
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A database migrated only to the version before the chain exists, so that adding an event fails at the server.</summary>
    internal static async Task<(ScratchDatabase Database, NpgsqlDataSource DataSource)> DatabaseWithoutTableAsync()
    {
        var made = await ScratchDatabase.CreateAsync();
        _ = await made.MigrateAsync([.. EmbeddedMigrations.Load().Where(step => step.Version < 8)]);
        return (made, NpgsqlDataSource.Create(made.AppConnectionString));
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
}
