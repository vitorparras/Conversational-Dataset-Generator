using System.Data.Common;
using System.Globalization;
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
    public Task<RecordedVerification?> LastAsync(CancellationToken cancellationToken) => LastOfAsync(CustodyStep.VerifyChain, cancellationToken);

    /// <summary>The last event of the chain that records a conference of the guarantees; null when the chain holds none.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public async Task<RecordedGuarantees?> LastGuaranteesAsync(CancellationToken cancellationToken)
    {
        var last = await LastOfAsync(CustodyStep.VerifyGuarantees, cancellationToken).ConfigureAwait(false);
        return last is null ? null : new RecordedGuarantees(last.RecordedAt, last.Passed, last.EventsRead);
    }

    /// <summary>The window of the owner credential that is open, or null when none is or the last event of the window closed it with every guarantee held.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public async Task<OwnerWindowOpen?> OwnerWindowAsync(CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(CustodyStatements.ReadOpenOwnerWindow);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await OpenedFromAsync(reader, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<OwnerWindowOpen?> OpenedFromAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var reference = (string)reader["reference"];
        var late = reference.Contains(";late=1", StringComparison.Ordinal);
        return new OwnerWindowOpen((long)reader["sequence"], reader.GetFieldValue<DateTimeOffset>(1), (int)Number(reference, "from"), (int)Number(reference, "to"), Number(reference, "tip"), late, (bool)reader["closing_failed"]);
    }

    private static long Number(string reference, string name) =>
        reference.Split(';').Select(piece => piece.Split('=')).Where(pair => pair.Length == 2 && pair[0] == name)
            .Select(pair => long.TryParse(pair[1], NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0L).FirstOrDefault();

    private async Task<RecordedVerification?> LastOfAsync(CustodyStep step, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(CustodyStatements.ReadLastVerification);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.AddWithValue("step", NpgsqlDbType.Text, CustodyNames.Of(step));
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
