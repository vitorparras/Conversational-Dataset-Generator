using System.Data;
using System.Data.Common;
using Core.Persistence.Connection;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Runs one guarantee's reading with the application's credential and turns what can go wrong on the way into a
/// refusal that says what to change; neither the connection string nor the server's own error text reaches the message.
/// </summary>
internal static class ServerProbe
{
    private const string InsufficientPrivilege = "42501";
    private const string InvalidCatalogName = "3D000";
    private const string CatalogUnreadable =
        "o papel da aplicação não pode ler o catálogo do sistema que a conferência consulta: devolva a leitura do catálogo a PUBLIC";

    // Operators and types resolve through the search path, and qualifying an operator in the text is not reasonable: the statement runs with the path
    // set to the catalog, whatever the role or the database set, on top of the qualified names in the text. LOCAL ends with the statement or the transaction around it.
    private const string PinSearchPath = "SET LOCAL search_path = pg_catalog, pg_temp";

    internal static async Task<CheckResult> RunAsync(NpgsqlDataSource dataSource, Guarantee guarantee, CancellationToken cancellationToken)
    {
        NpgsqlConnection connection;
        try
        {
            connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (NpgsqlException exception)
        {
            return NotConnected(guarantee.Name, exception);
        }

        await using (connection.ConfigureAwait(false))
        {
            return await ReadAsync(connection, guarantee, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<CheckResult> ReadAsync(NpgsqlConnection connection, Guarantee guarantee, CancellationToken cancellationToken)
    {
        var server = $"PostgreSQL {connection.ServerVersion}";
        try
        {
            var finding = await guarantee.ReadAsync(connection, cancellationToken).ConfigureAwait(false);
            return new CheckResult(guarantee.Name, finding.Status, finding.Message, DatabaseConfiguration.HostPath) { Observed = server };
        }
        catch (NpgsqlException exception)
        {
            var whatToChange = guarantee.ReadsCatalog && exception is PostgresException { SqlState: InsufficientPrivilege } ? CatalogUnreadable : guarantee.Requirement;
            return CheckResult.Failed(guarantee.Name, $"o servidor recusou a conferência{SqlState(exception)}: {whatToChange}", DatabaseConfiguration.HostPath) with { Observed = server };
        }
    }

    /// <summary>Runs the command, which answers one row, and hands the row to the judge; the search path is pinned to the catalog for the statement.</summary>
    internal static async Task<Finding> ReadRowAsync(
        NpgsqlConnection connection, Func<NpgsqlConnection, NpgsqlCommand> query, Func<IDataRecord, Finding> judge, CancellationToken cancellationToken)
    {
        var command = query(connection);
        await using (command.ConfigureAwait(false))
        {
            var batch = new NpgsqlBatch(connection) { BatchCommands = { new NpgsqlBatchCommand(PinSearchPath), new NpgsqlBatchCommand(command.CommandText) } };
            await using (batch.ConfigureAwait(false))
            {
                return await JudgeRowAsync(batch, judge, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<Finding> JudgeRowAsync(NpgsqlBatch batch, Func<IDataRecord, Finding> judge, CancellationToken cancellationToken)
    {
        var reader = await batch.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            _ = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            return judge(reader);
        }
    }

    private static CheckResult NotConnected(string name, NpgsqlException exception) =>
        exception is PostgresException refusal
            ? CheckResult.Failed(
                name,
                $"o servidor recusou a conexão da aplicação{SqlState(exception)}: confira o banco, o papel e a senha configurados",
                KeyToCheck(refusal))
            : CheckResult.Failed(
                name, "o servidor de banco não respondeu: confira o endereço configurado e se o servidor aceita conexões", DatabaseConfiguration.HostPath);

    private static string KeyToCheck(DbException refusal) =>
        refusal.SqlState == InvalidCatalogName ? DatabaseConfiguration.NamePath : DatabaseConfiguration.UsernamePath;

    private static string SqlState(NpgsqlException exception) =>
        exception is PostgresException refusal ? $" (SQLSTATE {refusal.SqlState})" : string.Empty;
}
