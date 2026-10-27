using Npgsql;

namespace Core.Persistence.Migrations;

/// <summary>Adds a row to the failure table for a step that failed, when the table exists; it never changes how the run ends.</summary>
internal static class FailureRegistry
{
    internal const string Table = "public.schema_failure";

    internal const string Record = "INSERT INTO " + Table + " (version, name, sha256, sqlstate) VALUES (@version, @name, @sha256, @sqlstate)";

    private const string Exists = "SELECT to_regclass('" + Table + "') IS NOT NULL";

    /// <summary>Whether the row was written; false when the table is not there yet or the server refused the insert.</summary>
    internal static async Task<bool> TryRecordAsync(NpgsqlConnection connection, MigrationStep step, string? sqlState, CancellationToken cancellationToken)
    {
        try
        {
            if (!await TableExistsAsync(connection, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            await InsertAsync(connection, step, sqlState, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (NpgsqlException)
        {
            return false;
        }
    }

    private static async Task<bool> TableExistsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var exists = new NpgsqlCommand(Exists, connection);
        await using (exists.ConfigureAwait(false))
        {
            return (bool)(await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        }
    }

    private static async Task InsertAsync(NpgsqlConnection connection, MigrationStep step, string? sqlState, CancellationToken cancellationToken)
    {
        var insert = new NpgsqlCommand(Record, connection);
        await using (insert.ConfigureAwait(false))
        {
            _ = insert.Parameters.AddWithValue("version", step.Version);
            _ = insert.Parameters.AddWithValue("name", step.Name);
            _ = insert.Parameters.AddWithValue("sha256", step.Sha256);
            _ = insert.Parameters.AddWithValue("sqlstate", (object?)sqlState ?? DBNull.Value);
            _ = await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
