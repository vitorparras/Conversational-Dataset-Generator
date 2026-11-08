using System.Data.Common;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.People;

public sealed partial class PersonRegistry
{
    private const string Narrowing = """
        WHERE (@name::text IS NULL OR position(lower(@name::text) in lower(name)) > 0)
          AND (@function::text IS NULL OR function_name = @function::text)
          AND (@active::boolean IS NULL OR active = @active::boolean)
        """;

    private const string CountMatching = "SELECT count(*) FROM public.person " + Narrowing;

    private const string PageMatching = "SELECT id, name, function_name, active FROM public.person " + Narrowing + " ORDER BY lower(name), id LIMIT @size OFFSET @skip";

    /// <summary>
    /// One page of the people the filter leaves, by name, with how many it leaves; the page asked beyond the last is the last, and one below the first is the first.
    /// It reads only the name, the function and the state, which are what the list shows.
    /// </summary>
    /// <param name="filter">What narrows the list.</param>
    /// <param name="page">The page, counted from one.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<PersonPage> ListAsync(PersonFilter filter, int page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return PageAsync(filter, page, cancellationToken);
    }

    private async Task<PersonPage> PageAsync(PersonFilter filter, int page, CancellationToken cancellationToken)
    {
        var total = await CountAsync(filter, cancellationToken).ConfigureAwait(false);
        var shown = Math.Clamp(page, 1, PersonPage.PagesFor(total));
        var command = dataSource.CreateCommand(PageMatching);
        await using (command.ConfigureAwait(false))
        {
            Bind(command, filter);
            _ = command.Parameters.AddWithValue("size", NpgsqlDbType.Integer, PersonPage.Size);
            _ = command.Parameters.AddWithValue("skip", NpgsqlDbType.Integer, (shown - 1) * PersonPage.Size);
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return new PersonPage(await ListedAsync(reader, cancellationToken).ConfigureAwait(false), total, shown);
            }
        }
    }

    private async Task<int> CountAsync(PersonFilter filter, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(CountMatching);
        await using (command.ConfigureAwait(false))
        {
            Bind(command, filter);
            return (int)(long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        }
    }

    private static void Bind(NpgsqlCommand command, PersonFilter filter)
    {
        _ = command.Parameters.AddWithValue("name", NpgsqlDbType.Text, string.IsNullOrWhiteSpace(filter.Name) ? DBNull.Value : filter.Name.Trim());
        _ = command.Parameters.AddWithValue("function", NpgsqlDbType.Text, (object?)filter.Function ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("active", NpgsqlDbType.Boolean, filter.Active.HasValue ? filter.Active.Value : DBNull.Value);
    }

    private static async Task<IReadOnlyList<ListedPerson>> ListedAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var people = new List<ListedPerson>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            people.Add(new ListedPerson((long)reader["id"], (string)reader["name"], (string)reader["function_name"], (bool)reader["active"]));
        }

        return people;
    }
}
