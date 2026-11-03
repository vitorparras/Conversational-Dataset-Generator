using Npgsql;

namespace Core.Persistence.Domains;

/// <summary>The labels registered in derivation_label, which the rotation grows: new content is sealed under the newest generation of its domain among them.</summary>
public static class RegisteredLabels
{
    /// <summary>The text of every label registered, as of this reading: a rotation made afterwards is seen only by a host that reads again, or restarts.</summary>
    public static Task<IReadOnlySet<string>> ReadAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        return ReadAllAsync(dataSource, cancellationToken);
    }

    private static async Task<IReadOnlySet<string>> ReadAllAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand("SELECT label FROM public.derivation_label");
        await using (command.ConfigureAwait(false))
        {
            return await TextsOfAsync(command, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<IReadOnlySet<string>> TextsOfAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        var registered = new HashSet<string>(StringComparer.Ordinal);
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                _ = registered.Add(reader.GetString(0));
            }
        }

        return registered;
    }
}
