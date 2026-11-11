using System.Data.Common;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Content.Emptying;

/// <summary>
/// Reads the record of an item that was emptied: the table of the items emptied says that it was, and the event of the chain says when, by whom, why and for which protocol.
/// It never reads the table of the texts or the one of the maps, which the server's audit counts as the reading of a text, so it cannot say that an item that was not emptied exists:
/// for such an item it answers none, as it does for an item of another domain, and the emptying itself says whether the item is there.
/// </summary>
public sealed class EmptiedItemReader
{
    private const int AtColumn = 0;
    private const int ExecutorColumn = 1;
    private const int ReasonColumn = 2;
    private const int ReferenceColumn = 3;

    private readonly NpgsqlDataSource dataSource;

    /// <summary>The reader goes through the data source built with the application's credential.</summary>
    /// <param name="dataSource">The source of the connections.</param>
    public EmptiedItemReader(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <summary>The record of the emptying of the item of the domain; none when the item was not emptied, does not exist or belongs to another domain.</summary>
    /// <param name="domainId">The identifier of the domain the item is looked for in.</param>
    /// <param name="itemId">The identifier of the item in <c>content.item_text</c>.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public async Task<EmptiedItem?> ReadAsync(int domainId, long itemId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(domainId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(itemId);
        var transaction = await ContentTransaction.BeginAsync(dataSource, domainId, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            return await ReadInAsync(transaction, itemId, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<EmptiedItem?> ReadInAsync(ContentTransaction transaction, long itemId, CancellationToken cancellationToken)
    {
        var command = transaction.CreateCommand(ContentQueries.ReadEmptiedItem);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = itemId });
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? await RecordAsync(reader, cancellationToken).ConfigureAwait(false) : null;
            }
        }
    }

    private static async Task<EmptiedItem> RecordAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var at = await reader.GetFieldValueAsync<DateTimeOffset>(AtColumn, cancellationToken).ConfigureAwait(false);
        return new EmptiedItem(at, reader.GetString(ExecutorColumn), MotiveOf(reader.GetString(ReasonColumn)), reader.GetString(ReferenceColumn));
    }

    private static EmptyingMotive MotiveOf(string reason) => reason switch
    {
        "removal_requested_by_subject" => EmptyingMotive.RemovalRequestedBySubject,
        "contract_requirement" => EmptyingMotive.ContractRequirement,
        _ => throw new InvalidOperationException("The event of an emptying carries a reason that is not a motive."),
    };
}
