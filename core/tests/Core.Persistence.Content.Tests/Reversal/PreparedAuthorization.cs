using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Reversal;

/// <summary>
/// An authorization of a request of the domain 1, with its event, in a serializable transaction that is prepared and not committed: the writer held in its commit, past the check
/// of its conflicts and not yet seen by any other. The disposal rolls it back unless it was committed, so that no prepared transaction outlives the test.
/// </summary>
internal sealed class PreparedAuthorization : IAsyncDisposable
{
    private readonly string connectionString;
    private readonly string name;
    private bool committed;

    private PreparedAuthorization(string connectionString, string name)
    {
        (this.connectionString, this.name) = (connectionString, name);
    }

    /// <summary>Authorizes the request by the person for seven days, as the application, and prepares the transaction.</summary>
    public static async Task<PreparedAuthorization> PrepareAsync(ContentWorld world, long request, long personId)
    {
        var name = $"zqx-autorizacao-{Guid.NewGuid():N}";
        await using (var connection = await Sql.OpenAsync(world.Database.AppConnectionString))
        {
            await RunAsync(connection, "BEGIN ISOLATION LEVEL SERIALIZABLE");
            await RunAsync(connection, "SELECT set_config('app.domain', '1', true)");
            await RunAsync(connection, ReversalRows.Authorization, ("domain", 1), ("request", request), ("person", personId), ("term", "7 days"));
            await RunAsync(connection, $"PREPARE TRANSACTION '{name}'");
        }

        return new PreparedAuthorization(world.Database.AppConnectionString, name);
    }

    /// <summary>Commits the prepared transaction: from here the authorization is seen.</summary>
    public async Task CommitAsync()
    {
        await EndAsync("COMMIT");
        committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!committed)
        {
            await EndAsync("ROLLBACK");
        }
    }

    private async Task EndAsync(string verb)
    {
        await using var connection = await Sql.OpenAsync(connectionString);
        await RunAsync(connection, $"{verb} PREPARED '{name}'");
    }

    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of this class and the name of the transaction is a GUID it drew, never input.")]
    private static async Task RunAsync(NpgsqlConnection connection, string statement, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(statement, connection);
        foreach (var (parameterName, value) in parameters)
        {
            _ = command.Parameters.AddWithValue(parameterName, value);
        }

        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
