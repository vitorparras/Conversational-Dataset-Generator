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

    // One statement, so the total and the page are read from the same snapshot; the page is clamped there, and with nobody left the one row it answers has no person.
    private const string PageMatching = """
        WITH matching AS (
            SELECT id, name, function_name, active, count(*) OVER () AS counted, row_number() OVER (ORDER BY lower(name), id) AS place
            FROM public.person
        """ + " " + Narrowing + """
        ),
        shown AS (
            SELECT coalesce(max(counted), 0)::integer AS total,
                   LEAST(GREATEST(@page, 1), GREATEST(1, (coalesce(max(counted), 0)::integer + @size - 1) / @size)) AS page
            FROM matching
        )
        SELECT shown.total, shown.page, matching.id, matching.name, matching.function_name, matching.active
        FROM shown LEFT JOIN matching ON matching.place > (shown.page - 1) * @size AND matching.place <= shown.page * @size
        ORDER BY matching.place
        """;

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
        var command = dataSource.CreateCommand(PageMatching);
        await using (command.ConfigureAwait(false))
        {
            Bind(command, filter);
            _ = command.Parameters.AddWithValue("size", NpgsqlDbType.Integer, PersonPage.Size);
            _ = command.Parameters.AddWithValue("page", NpgsqlDbType.Integer, page);
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await PageOfAsync(reader, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static void Bind(NpgsqlCommand command, PersonFilter filter)
    {
        _ = command.Parameters.AddWithValue("name", NpgsqlDbType.Text, string.IsNullOrWhiteSpace(filter.Name) ? DBNull.Value : filter.Name.Trim());
        _ = command.Parameters.AddWithValue("function", NpgsqlDbType.Text, (object?)filter.Function ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("active", NpgsqlDbType.Boolean, filter.Active.HasValue ? filter.Active.Value : DBNull.Value);
    }

    private static async Task<PersonPage> PageOfAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var people = new List<ListedPerson>();
        var (total, shown) = (0, 1);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            (total, shown) = ((int)reader["total"], (int)reader["page"]);
            if (reader["id"] is long id)
            {
                people.Add(new ListedPerson(id, (string)reader["name"], (string)reader["function_name"], (bool)reader["active"]));
            }
        }

        return new PersonPage(people, total, shown);
    }
}
