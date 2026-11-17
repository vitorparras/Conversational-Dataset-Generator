using Core.Persistence.Content;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Ingestion;

/// <summary>
/// What the reading of the text of an item of the sample asks of the database inside the transaction of the chain:
/// which step the item takes, and the sealed text with the key that opens it.
/// </summary>
internal static class SampleTextStatements
{
    private const int StepColumn = 0;
    private const int SealedColumn = 1;
    private const int EpochColumn = 2;
    private const int WrappedColumn = 3;

    private static NpgsqlCommand Command(CustodyScope scope, string statement, SampleReading request)
    {
        var command = scope.CreateCommand(statement);
        _ = command.Parameters.Add(ReviewStatements.Id("batch", request.BatchId));
        _ = command.Parameters.Add(new NpgsqlParameter("domain", NpgsqlDbType.Integer) { Value = request.DomainId });
        _ = command.Parameters.Add(ReviewStatements.Id("item", request.ItemId));
        return command;
    }

    internal static async Task<string?> StepAsync(CustodyScope scope, SampleReading request, CancellationToken cancellationToken)
    {
        var command = Command(scope, ContentQueries.ReadSampleStep, request);
        await using (command.ConfigureAwait(false))
        {
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        }
    }

    internal static async Task<(string Step, byte[] Sealed, ContentKey Key)?> SealedAsync(CustodyScope scope, SampleReading request, CancellationToken cancellationToken)
    {
        var command = Command(scope, ContentQueries.ReadSampleText, request);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                    ? (reader.GetString(StepColumn), (byte[])reader.GetValue(SealedColumn), new ContentKey(reader.GetInt32(EpochColumn), (byte[])reader.GetValue(WrappedColumn)))
                    : null;
            }
        }
    }
}
