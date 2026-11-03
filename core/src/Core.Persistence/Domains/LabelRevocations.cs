using Npgsql;

namespace Core.Persistence.Domains;

/// <summary>The labels revoked, read with the application's role: what encrypts new content must not use them, and what was encrypted under them is opened all the same.</summary>
public static class LabelRevocations
{
    private const string Query = "SELECT label FROM public.derivation_label_revocation";

    /// <summary>The text of every label revoked, as of this reading; a revocation made afterwards is seen by a reading made afterwards.</summary>
    /// <param name="dataSource">The data source of the application's credential.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public static Task<IReadOnlySet<string>> ReadAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        return LoadAsync(dataSource, cancellationToken);
    }

    private static async Task<IReadOnlySet<string>> LoadAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        var revoked = new HashSet<string>(StringComparer.Ordinal);
        var command = dataSource.CreateCommand(Query);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    _ = revoked.Add(reader.GetString(0));
                }
            }
        }

        return revoked;
    }
}
