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

    /// <summary>The window of the owner credential that is open, or null when the last event of the window closed it with every guarantee held, or no window was ever opened.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public async Task<OwnerWindowOpen?> OwnerWindowAsync(CancellationToken cancellationToken)
    {
        var last = await LastWindowEventAsync(cancellationToken).ConfigureAwait(false);
        if (last is not { } latest)
        {
            return null;
        }

        var closingFailed = latest.Reason == CustodyNames.Of(CustodyReason.VerificationFailed);
        return latest.Step == CustodyNames.Of(CustodyStep.OwnerWindowClosed) && !closingFailed
            ? null
            : await OpenedAsync(closingFailed, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(string Step, string Reason)?> LastWindowEventAsync(CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(CustodyStatements.ReadLastOwnerWindowEvent);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ((string)reader["step"], (string)reader["reason"]) : null;
            }
        }
    }

    private async Task<OwnerWindowOpen?> OpenedAsync(bool closingFailed, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(CustodyStatements.ReadLastOwnerWindowOpened);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await OpenedFromAsync(reader, closingFailed, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<OwnerWindowOpen?> OpenedFromAsync(DbDataReader reader, bool closingFailed, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var fields = OwnerWindowReference.Fields((string)reader["reference"]);
        var openedAt = await reader.GetFieldValueAsync<DateTimeOffset>(1, cancellationToken).ConfigureAwait(false);
        return new OwnerWindowOpen((long)reader["sequence"], openedAt, (int)Number(fields, "from"), (int)Number(fields, "to"), Number(fields, "tip"), fields.ContainsKey("late"), closingFailed);
    }

    private static long Number(IDictionary<string, string> fields, string name) =>
        fields.TryGetValue(name, out var value) && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0L;

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
