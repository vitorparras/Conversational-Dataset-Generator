using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.People;

public sealed partial class PersonRegistry
{
    private const string NamesOfIds = "SELECT id, name FROM public.person WHERE id = ANY(@ids)";

    /// <summary>
    /// The names of use of the people with the identifiers given, active or not, read in one query, so that a list says who did something without one reading for each line.
    /// An identifier nobody has is left out, and the one who shows the list says so. It does not authenticate.
    /// </summary>
    /// <param name="ids">The identifiers, which may repeat.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<IReadOnlyDictionary<long, string>> NamesOfAsync(IEnumerable<long> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return NamingAsync(ids.Distinct().ToArray(), cancellationToken);
    }

    private async Task<IReadOnlyDictionary<long, string>> NamingAsync(long[] ids, CancellationToken cancellationToken)
    {
        var names = new Dictionary<long, string>();
        if (ids.Length == 0)
        {
            return names;
        }

        var command = dataSource.CreateCommand(NamesOfIds);
        await using (command.ConfigureAwait(false))
        {
            Add(command, "ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint, ids);
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    names[reader.GetInt64(0)] = reader.GetString(1);
                }
            }
        }

        return names;
    }
}
