using System.Data.Common;
using System.Globalization;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Custody.Verification;

/// <summary>Reads the last verification the chain recorded; what a process remembers of its own verifications is not asked, since only the chain says what was accepted.</summary>
public sealed class RecordedVerificationReader
{
    private const int PairParts = 2;

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
    public async Task<RecordedVerification?> LastAsync(CancellationToken cancellationToken) =>
        Newest(await ReadAsync(CustodyStep.VerifyChain, 1, cancellationToken).ConfigureAwait(false));

    /// <summary>The events of the chain that record a verification, the newest first, each with who asked for it when someone did; the host's own verifications have no asker.</summary>
    /// <param name="limit">How many to read at most; positive.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<IReadOnlyList<RecordedVerification>> HistoryAsync(int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        return ReadAsync(CustodyStep.VerifyChain, limit, cancellationToken);
    }

    /// <summary>The last event of the chain that records a conference of the guarantees; null when the chain holds none.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public async Task<RecordedGuarantees?> LastGuaranteesAsync(CancellationToken cancellationToken)
    {
        var last = Newest(await ReadAsync(CustodyStep.VerifyGuarantees, 1, cancellationToken).ConfigureAwait(false));
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
        var openedAt = await reader.GetFieldValueAsync<DateTimeOffset>(1, cancellationToken).ConfigureAwait(false);
        return new OwnerWindowOpen(
            (long)reader["sequence"], openedAt, (int)Number(reference, "from"), (int)Number(reference, "to"), Number(reference, "tip"), late, (bool)reader["closing_failed"]);
    }

    private static long Number(string reference, string name) =>
        reference.Split(';').Select(piece => piece.Split('=')).Where(pair => pair.Length == PairParts && pair[0] == name)
            .Select(pair => long.TryParse(pair[1], NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0L).FirstOrDefault();

    private async Task<IReadOnlyList<RecordedVerification>> ReadAsync(CustodyStep step, int limit, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(CustodyStatements.ReadLastVerification);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.AddWithValue("step", NpgsqlDbType.Text, CustodyNames.Of(step));
            _ = command.Parameters.AddWithValue("limit", NpgsqlDbType.Integer, limit);
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await AllAsync(reader, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static RecordedVerification? Newest(IReadOnlyList<RecordedVerification> newestFirst) => newestFirst.Count == 0 ? null : newestFirst[0];

    private static async Task<IReadOnlyList<RecordedVerification>> AllAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var all = new List<RecordedVerification>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var recordedAt = await reader.GetFieldValueAsync<DateTimeOffset>(0, cancellationToken).ConfigureAwait(false);
            var passed = (string)reader["reason"] == CustodyNames.Of(CustodyReason.VerificationPassed);
            var asker = RequesterReference.From(reader["reference"] as string);
            all.Add(new RecordedVerification(recordedAt, passed, (long)reader["item_id"], reader["corrects"] is long first ? first : null, asker, asker is null ? null : reader["asker"] as string));
        }

        return all;
    }
}
