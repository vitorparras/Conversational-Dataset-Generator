using System.Data.Common;
using Npgsql;

namespace Core.Persistence.Domains;

/// <summary>Reads the domains the installation has, in the order they were made; it changes nothing.</summary>
public sealed class DomainListing
{
    private const string All = "SELECT id, name, created_at FROM public.domain ORDER BY id";

    private readonly NpgsqlDataSource dataSource;

    /// <summary>The reading goes through the data source built with the application's credential.</summary>
    /// <param name="dataSource">The source of the connections.</param>
    public DomainListing(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <summary>Every domain, by identifier; empty when none was created.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public async Task<IReadOnlyList<ListedDomain>> ListAsync(CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(All);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await ReadAllAsync(reader, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<IReadOnlyList<ListedDomain>> ReadAllAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var domains = new List<ListedDomain>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var createdAt = await reader.GetFieldValueAsync<DateTimeOffset>(reader.GetOrdinal("created_at"), cancellationToken).ConfigureAwait(false);
            domains.Add(new ListedDomain((int)reader["id"], (string)reader["name"], createdAt, TaxonomyVersion: null, DimensionsVersion: null));
        }

        return domains;
    }
}
