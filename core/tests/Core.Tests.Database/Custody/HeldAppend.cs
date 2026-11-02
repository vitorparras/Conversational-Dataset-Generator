using System.Data;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>An event added to the chain by a transaction that is kept open, so that a second writer reaching the same link has to wait for how this one ends.</summary>
internal sealed class HeldAppend : IAsyncDisposable
{
    private const string Statement = """
        INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason)
        VALUES (true, 'person', @item, 'person_created', 'administrative_act')
        """;

    private readonly NpgsqlConnection connection;
    private readonly NpgsqlTransaction transaction;

    private HeldAppend(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        this.connection = connection;
        this.transaction = transaction;
    }

    internal static async Task<HeldAppend> BeginAsync(string connectionString, long item, IsolationLevel isolation)
    {
        var connection = await Sql.OpenAsync(connectionString);
        var transaction = await connection.BeginTransactionAsync(isolation, TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(Statement, connection, transaction);
        _ = command.Parameters.AddWithValue("item", item);
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return new HeldAppend(connection, transaction);
    }

    internal Task CommitAsync() => transaction.CommitAsync(TestContext.Current.CancellationToken);

    internal Task RollbackAsync() => transaction.RollbackAsync(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        await transaction.DisposeAsync();
        await connection.DisposeAsync();
    }
}
