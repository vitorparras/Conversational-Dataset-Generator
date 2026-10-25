using System.Data;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>Checks that the server opens a serializable transaction for the application's role and runs a query in it.</summary>
public sealed class SerializableCheck : IStartupCheck
{
    private const string Serializable = "serializable";
    private const string Query = "SELECT current_setting('transaction_isolation')";

    private readonly NpgsqlDataSource dataSource;

    /// <summary>The check reads through the data source built with the application's credential.</summary>
    public SerializableCheck(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <inheritdoc />
    public string Name => "Serializable";

    /// <inheritdoc />
    public Task<CheckResult> RunAsync(CancellationToken cancellationToken) =>
        ServerProbe.RunAsync(
            dataSource,
            new Guarantee(
                Name, "conecte a aplicação a um servidor primário, que aceita transação SERIALIZABLE; réplica em espera não aceita", ReadAsync),
            cancellationToken);

    internal static Finding Judge(string isolation) =>
        isolation == Serializable
            ? Finding.Holds("o servidor abre transação SERIALIZABLE para o papel da aplicação")
            : Finding.Missing(
                $"o servidor abriu em '{isolation}' a transação pedida como SERIALIZABLE: conecte a aplicação direto ao servidor "
                + "primário, sem intermediário que troque o isolamento");

    private static async Task<Finding> ReadAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            return await ServerProbe.ReadRowAsync(
                connection, server => new NpgsqlCommand(Query, server), row => Judge((string)row[0]), cancellationToken).ConfigureAwait(false);
        }
    }
}
