using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// A login of its own that is a member of the application's role or of the role owner and may set <c>session_replication_role</c>, so that a session of either credential
/// can run as a replica, which skips every trigger not enabled ALWAYS. The login lives on the server, not in the database, and is taken back on dispose.
/// </summary>
[SuppressMessage("Security", "CA2100", Justification = "The statements are literals of this class with names it made itself, never input.")]
internal sealed class ReplicaSession : IAsyncDisposable
{
    private const long ParameterPrivilegeLock = 5_900_001;

    private readonly string role;

    private ReplicaSession(string role, string connectionString)
    {
        this.role = role;
        ConnectionString = connectionString;
    }

    /// <summary>The connection string of the login, to the database of the test, that starts every session as a replica.</summary>
    public string ConnectionString { get; }

    /// <summary>A login that inherits the credential of <paramref name="member"/> (app or owner).</summary>
    public static async Task<ReplicaSession> OfAsync(CustodyDatabase database, string member)
    {
        var role = $"zqx_replica_{Guid.NewGuid():N}";
        var password = Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture);
        await RunSerialAsync($"CREATE ROLE {role} LOGIN PASSWORD '{password}' IN ROLE {member}; GRANT SET ON PARAMETER session_replication_role TO {role}");
        var connection = new NpgsqlConnectionStringBuilder(member == "owner" ? await database.OwnerConnectionStringAsync() : await database.AppConnectionStringAsync())
        {
            Username = role,
            Password = password,
            Options = "-c session_replication_role=replica",
        };
        return new ReplicaSession(role, connection.ConnectionString);
    }

    /// <summary>What a session of the connection string reports as its replication role, to prove that a refusal seen in it was met in a replica.</summary>
    public static async Task<string> ModeOfAsync(string connectionString)
    {
        await using var connection = await Sql.OpenAsync(connectionString);
        await using var command = new NpgsqlCommand("SHOW session_replication_role", connection);
        return (string)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    public async ValueTask DisposeAsync() =>
        await RunSerialAsync($"REVOKE SET ON PARAMETER session_replication_role FROM {role}; DROP ROLE {role}");

    /// <summary>
    /// Runs the statements as the superuser in one transaction that holds an advisory lock first. The privilege on a parameter is one row of the cluster,
    /// made by the first grant and removed by the last revoke,
    /// so two sessions that grant or revoke at once meet a duplicate key; the lock is taken in the shared database because an advisory lock belongs to
    /// the database it is taken in, and the scratch databases
    /// of the classes that run at once are many.
    /// </summary>
    private static async Task RunSerialAsync(string statements)
    {
        await using var connection = await Sql.OpenAsync((await PostgresFixture.SharedAsync()).SuperuserConnectionString);
        await using var command = new NpgsqlCommand($"SELECT pg_catalog.pg_advisory_xact_lock({ParameterPrivilegeLock}); {statements}", connection);
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
