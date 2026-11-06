namespace Core.Persistence.People;

public sealed partial class PersonRegistry
{
    /// <summary>Whether the registry has nobody at all, active or not, which is the one state where the first person can be made. It does not authenticate.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public async Task<bool> IsEmptyAsync(CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(PersonStatements.NobodyRegistered);
        await using (command.ConfigureAwait(false))
        {
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true;
        }
    }
}
