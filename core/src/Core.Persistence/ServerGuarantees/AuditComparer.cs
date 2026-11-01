using Core.Persistence.Custody;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Reads the two layers for an interval, the server's audit log and the events of the chain, and sets one against the other. It only reads: the
/// chain through the application's own credential, the log from the folder the server writes it to, of the one database that credential opens
/// since the server's log holds every database the server serves.
/// </summary>
public sealed class AuditComparer
{
    private const string CountSteps = """
        SELECT step, pg_catalog.count(*) FROM public.custody_event WHERE occurred_at >= @from AND occurred_at < @to GROUP BY step
        """;

    private readonly NpgsqlDataSource dataSource;
    private readonly ServerAuditLog log;
    private readonly string applicationRole;
    private readonly string database;

    /// <summary>The comparison runs in the role the data source connects as, which is the role the application's commands run in; the log is read from the folder.</summary>
    public AuditComparer(NpgsqlDataSource dataSource, string serverLogDirectory)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(serverLogDirectory);
        this.dataSource = dataSource;
        log = new ServerAuditLog(serverLogDirectory);
        var connection = new NpgsqlConnectionStringBuilder(dataSource.ConnectionString);
        applicationRole = connection.Username ?? throw new ArgumentException("A fonte de dados não nomeia o papel da aplicação.", nameof(dataSource));
        database = connection.Database ?? throw new ArgumentException("A fonte de dados não nomeia o banco.", nameof(dataSource));
    }

    /// <summary>The differences between the layers from <paramref name="from"/> (inclusive) to <paramref name="to"/> (exclusive).</summary>
    public async Task<IReadOnlyList<AuditDifference>> CompareAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var seen = (await log.ReadAsync(from, to, cancellationToken).ConfigureAwait(false)).Where(entry => entry.Database == database).ToList();
        var recorded = await RecordedAsync(from, to, cancellationToken).ConfigureAwait(false);
        return AuditComparison.Compare(seen, applicationRole, recorded);
    }

    private async Task<Dictionary<CustodyStep, int>> RecordedAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(CountSteps);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.AddWithValue("from", from);
            _ = command.Parameters.AddWithValue("to", to);
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await CountsAsync(reader, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<Dictionary<CustodyStep, int>> CountsAsync(System.Data.Common.DbDataReader reader, CancellationToken cancellationToken)
    {
        var byName = Enum.GetValues<CustodyStep>().ToDictionary(CustodyNames.Of, StringComparer.Ordinal);
        var recorded = new Dictionary<CustodyStep, int>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            recorded[byName[reader.GetString(0)]] = checked((int)reader.GetInt64(1));
        }

        return recorded;
    }
}
