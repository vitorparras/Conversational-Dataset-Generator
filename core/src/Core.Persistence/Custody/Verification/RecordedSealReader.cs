using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Custody.Verification;

/// <summary>Reads when the chain last recorded a seal, which is how old the seal a check is made against can be; only the chain says what was recorded.</summary>
public sealed class RecordedSealReader
{
    private readonly NpgsqlDataSource dataSource;

    /// <summary>The reading goes through the data source built with the application's credential.</summary>
    /// <param name="dataSource">The source of the connections.</param>
    public RecordedSealReader(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <summary>The moment the server recorded the last seal; null when the chain holds none.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public async Task<DateTimeOffset?> LastAsync(CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(CustodyStatements.ReadLastSeal);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.AddWithValue("step", NpgsqlDbType.Text, CustodyNames.Of(CustodyStep.SealRecorded));
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                    ? await reader.GetFieldValueAsync<DateTimeOffset>(0, cancellationToken).ConfigureAwait(false)
                    : null;
            }
        }
    }
}
