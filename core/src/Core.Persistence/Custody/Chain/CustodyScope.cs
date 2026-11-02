using Npgsql;

namespace Core.Persistence.Custody.Chain;

/// <summary>The serializable transaction in which a change and its custody event are written together; its commands run in it.</summary>
internal sealed class CustodyScope
{
    private readonly NpgsqlConnection connection;
    private readonly NpgsqlTransaction transaction;

    internal CustodyScope(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        this.connection = connection;
        this.transaction = transaction;
    }

    internal NpgsqlCommand CreateCommand(string sql) => new(sql, connection, transaction);

    internal Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
}
