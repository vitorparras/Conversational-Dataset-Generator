using System.Data;
using Core.Persistence.Custody.Chain;
using Core.Persistence.People;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Content.Rerun;

/// <summary>The readings of the queue, and the writing of a request: rows into records, with no judgment of what they mean.</summary>
internal static class RerunReading
{
    private const int ThirdColumn = 2;

    /// <summary>Whether a request to the version would queue an item.</summary>
    internal static async Task<bool> PendingAsync(CustodyScope scope, string version, CancellationToken cancellationToken) =>
        (await RowsAsync(scope, (ContentQueries.ReadRerunPending, command => PersonQueries.Add(command, "version", NpgsqlDbType.Text, version)), row => row.GetInt64(0), cancellationToken)
            .ConfigureAwait(false)).Count > 0;

    /// <summary>The newest open request; none when every request is concluded.</summary>
    internal static async Task<OpenRerun?> OpenAsync(CustodyScope scope, CancellationToken cancellationToken)
    {
        var open = await RowsAsync(scope, (ContentQueries.ReadRerunOpen, _ => { }), row => new OpenRerun(row.GetInt64(0), row.GetString(1)), cancellationToken).ConfigureAwait(false);
        return open.Count == 0 ? null : open[0];
    }

    /// <summary>Writes the request and queues its items; answers the request.</summary>
    internal static async Task<long> InsertAsync(CustodyScope scope, RerunRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(ContentQueries.InsertSanitizationRerun);
        await using (command.ConfigureAwait(false))
        {
            PersonQueries.Add(command, "domain", NpgsqlDbType.Integer, request.DomainId);
            PersonQueries.Add(command, "version", NpgsqlDbType.Text, request.Version);
            PersonQueries.Add(command, "steps", NpgsqlDbType.Text, request.Steps);
            PersonQueries.Add(command, "person", NpgsqlDbType.Bigint, request.PersonId);
            PersonQueries.Add(command, "now", NpgsqlDbType.TimestampTz, now);
            return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        }
    }

    /// <summary>Where the request of the domain stands; none when the domain has no such request.</summary>
    internal static async Task<RerunProgress?> ProgressAsync(NpgsqlDataSource dataSource, int domainId, long rerunId, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, domainId, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var scope = transaction.AsCustodyScope();
            void Bind(NpgsqlCommand command) => PersonQueries.Add(command, "rerun", NpgsqlDbType.Bigint, rerunId);
            var progress = await RowsAsync(scope, (ContentQueries.ReadRerunProgress, Bind), Progress, cancellationToken).ConfigureAwait(false);
            var refusals = await RowsAsync(scope, (ContentQueries.ReadRerunRefusals, Bind), Refusal, cancellationToken).ConfigureAwait(false);
            return progress.Count == 0 ? null : progress[0] with { Refusals = refusals };
        }
    }

    /// <summary>Every row the statement answers, bound by the action given and each read by the function given, read whole before anything else runs on the connection.</summary>
    internal static async Task<List<T>> RowsAsync<T>(CustodyScope scope, (string Statement, Action<NpgsqlCommand> Bind) query, Func<NpgsqlDataReader, T> read, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(query.Statement);
        await using var commandScope = command.ConfigureAwait(false);
        query.Bind(command);
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerScope = reader.ConfigureAwait(false);
        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(read(reader));
        }

        return rows;
    }

    // The columns in the order of the statement: version, moment, the two states, the four results and the last conclusion.
    private static RerunProgress Progress(NpgsqlDataReader row)
    {
        var at = 0;
        return new RerunProgress
        {
            TargetVersion = row.GetString(at++),
            RequestedAt = row.GetFieldValue<DateTimeOffset>(at++),
            Queued = row.GetInt64(at++),
            Held = row.GetInt64(at++),
            Identical = row.GetInt64(at++),
            Replaced = row.GetInt64(at++),
            Refused = row.GetInt64(at++),
            Superseded = row.GetInt64(at++),
            LastConcludedAt = row.IsDBNull(at) ? null : row.GetFieldValue<DateTimeOffset>(at),
            Refusals = [],
        };
    }

    private static RerunItemRefusal Refusal(IDataRecord row) => new(row.GetInt64(0), row.GetInt64(1), row.GetString(ThirdColumn) switch
    {
        "emptied" => RerunRefusal.Emptied,
        "key_lost" => RerunRefusal.KeyLost,
        "failed" => RerunRefusal.Failed,
        _ => throw new InvalidDataException("The queue keeps a refusal that is not one of the three."),
    });
}
