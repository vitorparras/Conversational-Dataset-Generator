using Npgsql;

namespace Core.Tests.Integration.Fixtures;

/// <summary>Opens a connection for the test that is running.</summary>
internal static class Sql
{
    public static async Task<NpgsqlConnection> OpenAsync(string connectionString)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }
}
