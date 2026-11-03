using System.Security.Cryptography;
using Core.Persistence.Content;
using Core.Persistence.Custody;
using Core.Platform.Keys;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// A small sample, the same every time: the first record of the raw text and of the map in the first domains that hold one, read as the system through the path that records the access
/// and opened under the label its envelope declares. Opening all would expose content for nothing.
/// </summary>
internal sealed class EncryptedSample
{
    private const string Domains = """
        SELECT coalesce(array_agg(domain_id ORDER BY domain_id), ARRAY[]::integer[])
        FROM (SELECT domain_id FROM public.content_domain WHERE table_name = @table ORDER BY domain_id LIMIT 3) first_domains
        """;

    private readonly NpgsqlDataSource dataSource;
    private readonly Func<CancellationToken, Task<MasterKeyReading>> read;
    private readonly Func<MasterKeyReading, ProtectedContentReader> open;

    internal EncryptedSample(NpgsqlDataSource dataSource, Func<CancellationToken, Task<MasterKeyReading>> read, Func<MasterKeyReading, ProtectedContentReader> open)
    {
        this.dataSource = dataSource;
        this.read = read;
        this.open = open;
    }

    /// <summary>Whether every record of the sample opened; a key not read, a label not loaded, a label that is not the column's and an access the chain did not record all say no.</summary>
    internal async Task<bool> HoldsAsync(CancellationToken cancellationToken)
    {
        using var reading = await read(cancellationToken).ConfigureAwait(false);
        if (reading.Failure is not null)
        {
            return false;
        }

        var reader = open(reading);
        var held = true;
        foreach (var column in new[] { ContentColumns.RawText, ContentColumns.Entries })
        {
            foreach (var domain in await DomainsAsync(column.Table, cancellationToken).ConfigureAwait(false))
            {
                held &= await OpenedAsync(reader, column, domain, cancellationToken).ConfigureAwait(false);
            }
        }

        return held;
    }

    private static async Task<bool> OpenedAsync(ProtectedContentReader reader, ProtectedColumn column, int domain, CancellationToken cancellationToken)
    {
        try
        {
            var read = await reader.ReadFirstAsync(CustodyAuthor.System, column, domain, cancellationToken).ConfigureAwait(false);
            return read.Outcome != ContentReadOutcome.NotRecorded;
        }
        catch (Exception refused) when (refused is EnvelopeRefusedException or CryptographicException)
        {
            return false;
        }
    }

    private async Task<int[]> DomainsAsync(string table, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(Domains);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.AddWithValue("table", NpgsqlDbType.Text, table);
            return (int[])(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        }
    }
}
