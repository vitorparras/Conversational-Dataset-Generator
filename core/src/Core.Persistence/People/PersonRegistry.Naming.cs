using NpgsqlTypes;

namespace Core.Persistence.People;

public sealed partial class PersonRegistry
{
    private const string NameOfId = "SELECT name FROM public.person WHERE id = @id";

    /// <summary>The name of use of the person, active or not; null when nobody has the identifier. It does not authenticate.</summary>
    /// <param name="personId">The identifier the registry gave.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public async Task<string?> NameOfAsync(long personId, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(NameOfId);
        await using (command.ConfigureAwait(false))
        {
            Add(command, "id", NpgsqlDbType.Bigint, personId);
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        }
    }
}
