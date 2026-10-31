using Core.Persistence.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>Disposing a content transaction ends the transaction itself and not only the connection that carries it.</summary>
[Trait("Category", "Database")]
public sealed class ContentTransactionTests
{
    [DatabaseFact]
    public async Task DisposingEndsTheTransactionItselfBeforeTheConnectionGoesBack()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        await using var dataSource = NpgsqlDataSource.Create(database.AppConnectionString);
        var transaction = await ContentTransaction.BeginAsync(dataSource, 1, TestContext.Current.CancellationToken);
        await using var command = transaction.CreateCommand("SELECT 1");
        var carried = command.Transaction!;

        await transaction.DisposeAsync();

        _ = Assert.Throws<ObjectDisposedException>(() => carried.IsolationLevel);
    }
}
