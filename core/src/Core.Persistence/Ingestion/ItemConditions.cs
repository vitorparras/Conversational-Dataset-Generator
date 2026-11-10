using Core.Persistence.Content;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Ingestion;

/// <summary>The condition under which an item came in: the one of the batch it belongs to, read beside the item and never copied from it.</summary>
public sealed class ItemConditions
{
    private readonly NpgsqlDataSource dataSource;

    /// <summary>The conditions are read through the data source built with the application's credential.</summary>
    public ItemConditions(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <summary>The condition of an item of the stock; null when the domain holds no such item among the ones that can be read (the item of a batch not accepted is not one).</summary>
    /// <param name="domainId">The domain of the item.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<string?> OfAsync(int domainId, long itemId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(domainId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(itemId);
        return ReadingAsync(domainId, itemId, cancellationToken);
    }

    private async Task<string?> ReadingAsync(int domainId, long itemId, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, domainId, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var command = transaction.CreateCommand(ContentQueries.ReadItemCondition);
            await using (command.ConfigureAwait(false))
            {
                _ = command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = itemId });
                return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
            }
        }
    }
}
