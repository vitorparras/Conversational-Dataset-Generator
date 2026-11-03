using System.Data.Common;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Custody.Verification;

/// <summary>Reads the last verification the chain recorded; what a process remembers of its own verifications is not asked, since only the chain says what was accepted.</summary>
public sealed class RecordedVerificationReader
{
    private readonly NpgsqlDataSource dataSource;

    /// <summary>The reading goes through the data source built with the application's credential.</summary>
    /// <param name="dataSource">The source of the connections.</param>
    public RecordedVerificationReader(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <summary>The last event of the chain that records a verification; null when the chain holds none.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public async Task<RecordedVerification?> LastAsync(CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(CustodyStatements.ReadLastVerification);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.AddWithValue("step", NpgsqlDbType.Text, CustodyNames.Of(CustodyStep.VerifyChain));
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await FirstAsync(reader, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<RecordedVerification?> FirstAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var recordedAt = await reader.GetFieldValueAsync<DateTimeOffset>(0, cancellationToken).ConfigureAwait(false);
        var passed = (string)reader["reason"] == CustodyNames.Of(CustodyReason.VerificationPassed);
        return new RecordedVerification(recordedAt, passed, (long)reader["item_id"], reader["corrects"] is long first ? first : null);
    }
}
