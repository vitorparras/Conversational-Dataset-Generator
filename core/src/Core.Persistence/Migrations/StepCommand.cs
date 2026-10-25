using Npgsql;

namespace Core.Persistence.Migrations;

/// <summary>Runs one step as a command without parameters, so the server's own client splits its statements.</summary>
internal static class StepCommand
{
    internal static async Task RunAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, MigrationStep step, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(step.Sql, connection, transaction);
        await using (command.ConfigureAwait(false))
        {
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
