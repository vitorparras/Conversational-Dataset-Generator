using System.Data.Common;
using Npgsql;

namespace Core.Persistence.Migrations;

/// <summary>Reads the registry in order of version; an absent table, which the first step creates, counts as no row.</summary>
internal static class SchemaRegistry
{
    internal const string Table = "public.schema_version";

    private const string Exists = "SELECT to_regclass('" + Table + "') IS NOT NULL";
    private const string Rows = "SELECT version, name, sha256 FROM " + Table + " ORDER BY version";

    internal static async Task<IReadOnlyList<AppliedStep>> ReadAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var exists = new NpgsqlCommand(Exists, connection);
        await using (exists.ConfigureAwait(false))
        {
            if (!(bool)(await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!)
            {
                return [];
            }
        }

        var command = new NpgsqlCommand(Rows, connection);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await AppliedAsync(reader, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<IReadOnlyList<AppliedStep>> AppliedAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var rows = new List<AppliedStep>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new AppliedStep((int)reader["version"], (string)reader["name"], (string)reader["sha256"]));
        }

        return rows;
    }
}
