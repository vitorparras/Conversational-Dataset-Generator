using Core.Persistence.Custody;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.People;

/// <summary>
/// The people who operate the system. Each operation writes the person, the record of what was done and the event of the chain of custody in one transaction, so that
/// the history never lacks a change nor holds one that did not happen. A person is deactivated and never deleted; what they reach comes from grants.
/// </summary>
public sealed partial class PersonRegistry
{
    private readonly NpgsqlDataSource dataSource;

    /// <summary>The registry writes through the data source built with the application's credential.</summary>
    public PersonRegistry(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <summary>Adds a person, active, with the function given; a refusal writes nothing.</summary>
    /// <param name="person">The name of use and the function.</param>
    /// <param name="author">Who acts: a person of the registry, or the system declared as such; never left out.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public Task<PersonResult> CreateAsync(NewPerson person, CustodyAuthor author, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(author);
        return AddAsync(person, author, cancellationToken);
    }

    private async Task<PersonResult> AddAsync(NewPerson person, CustodyAuthor author, CancellationToken cancellationToken)
    {
        if (PersonRefusals.Judge(person) is { } refusal)
        {
            return refusal;
        }

        try
        {
            var statement = new PersonStatement(
                PersonStatements.Create,
                command =>
                {
                    Add(command, "name", NpgsqlDbType.Text, person.Name);
                    Add(command, "function", NpgsqlDbType.Text, person.Function);
                    Add(command, "actor", NpgsqlDbType.Bigint, author.PersonId);
                },
                CustodyStep.PersonCreated);
            return await dataSource.WriteChainedAsync(statement, author, cancellationToken).ConfigureAwait(false)
                ?? await dataSource.ActorRefusalAsync(author.PersonId, cancellationToken).ConfigureAwait(false)
                ?? PersonRefusals.ActorDeactivated();
        }
        catch (PostgresException refused) when (refused.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return PersonRefusals.NameTaken();
        }
    }

    /// <summary>
    /// Adds the first person of the installation, by the system, and only if the registry has nobody: the check and the write are one statement in the serializable transaction of the chain,
    /// so that two commands at once make one person and the other is refused.
    /// </summary>
    /// <param name="person">The name of use and the function.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public Task<PersonResult> CreateFirstAsync(NewPerson person, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(person);
        return AddFirstAsync(person, cancellationToken);
    }

    private async Task<PersonResult> AddFirstAsync(NewPerson person, CancellationToken cancellationToken)
    {
        if (PersonRefusals.Judge(person) is { } refusal)
        {
            return refusal;
        }

        var statement = new PersonStatement(
            PersonStatements.CreateFirst,
            command =>
            {
                Add(command, "name", NpgsqlDbType.Text, person.Name);
                Add(command, "function", NpgsqlDbType.Text, person.Function);
            },
            CustodyStep.PersonCreated);
        return await dataSource.WriteChainedAsync(statement, CustodyAuthor.System, cancellationToken).ConfigureAwait(false) ?? PersonRefusals.RegistryNotEmpty();
    }

    /// <summary>Deactivates a person; the person stays in the registry and in the history.</summary>
    /// <param name="personId">The person to deactivate.</param>
    /// <param name="author">Who acts: a person of the registry, or the system declared as such; never left out.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public Task<PersonResult> DeactivateAsync(long personId, CustodyAuthor author, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(author);
        return RemoveAsync(personId, author, cancellationToken);
    }

    private async Task<PersonResult> RemoveAsync(long personId, CustodyAuthor author, CancellationToken cancellationToken)
    {
        var statement = new PersonStatement(
            PersonStatements.Deactivate,
            command =>
            {
                Add(command, "id", NpgsqlDbType.Bigint, personId);
                Add(command, "actor", NpgsqlDbType.Bigint, author.PersonId);
            },
            CustodyStep.PersonDeactivated,
            personId);
        return await dataSource.WriteChainedAsync(statement, author, cancellationToken).ConfigureAwait(false)
            ?? await WhyNotDeactivatedAsync(personId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deactivates a person as <see cref="DeactivateAsync" /> does, except the last active holder of the administration function, who is refused with their own outcome: the check and the write are
    /// one statement in the serializable transaction of the chain, so that two deactivations at once cannot leave the installation with none.
    /// </summary>
    /// <param name="personId">The person to deactivate.</param>
    /// <param name="author">Who acts: a person of the registry, or the system declared as such; never left out.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public Task<PersonResult> DeactivateKeepingAdministrationAsync(long personId, CustodyAuthor author, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(author);
        return RemoveKeepingAsync(personId, author, cancellationToken);
    }

    private async Task<PersonResult> RemoveKeepingAsync(long personId, CustodyAuthor author, CancellationToken cancellationToken)
    {
        var statement = new PersonStatement(
            PersonStatements.DeactivateKeepingAdministration,
            command =>
            {
                Add(command, "id", NpgsqlDbType.Bigint, personId);
                PersonQueries.AddActor(command, author.PersonId);
                Add(command, "administration", NpgsqlDbType.Text, PersonFunctions.Administration);
            },
            CustodyStep.PersonDeactivated,
            personId);
        return await dataSource.WriteChainedAsync(statement, author, cancellationToken).ConfigureAwait(false)
            ?? await WhyNotKeptAsync(personId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The function of an active person; null when nobody has the identifier or the person is deactivated. It does not authenticate.</summary>
    /// <param name="personId">The person whose function is asked.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public async Task<string?> ActiveFunctionOfAsync(long personId, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(PersonStatements.ActiveFunctionOf);
        await using (command.ConfigureAwait(false))
        {
            Add(command, "id", NpgsqlDbType.Bigint, personId);
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        }
    }

    /// <summary>Gives an active person another function, keeping the previous one in the history; a function outside the policy writes nothing.</summary>
    /// <param name="personId">The person whose function changes.</param>
    /// <param name="function">One of <see cref="PersonFunctions.Accepted" />.</param>
    /// <param name="author">Who acts: a person of the registry, or the system declared as such; never left out.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public Task<PersonResult> ChangeFunctionAsync(long personId, string function, CustodyAuthor author, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(author);
        return ReplaceFunctionAsync(personId, function, author, cancellationToken);
    }

    private async Task<PersonResult> ReplaceFunctionAsync(long personId, string function, CustodyAuthor author, CancellationToken cancellationToken)
    {
        if (PersonRefusals.NotAccepted(function) is { } refusal)
        {
            return refusal;
        }

        var statement = new PersonStatement(
            PersonStatements.ChangeFunction,
            command =>
            {
                Add(command, "id", NpgsqlDbType.Bigint, personId);
                Add(command, "function", NpgsqlDbType.Text, function);
                Add(command, "actor", NpgsqlDbType.Bigint, author.PersonId);
            },
            CustodyStep.PersonFunctionChanged,
            personId);
        return await dataSource.WriteChainedAsync(statement, author, cancellationToken).ConfigureAwait(false)
            ?? await WhyNotChangedAsync(personId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Whether a person may enter: registered and active. It does not authenticate; it only answers for the state of the person.</summary>
    /// <param name="personId">The person who asks to enter.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public async Task<EntryStatus> AdmitAsync(long personId, CancellationToken cancellationToken) =>
        await dataSource.ReadActiveAsync(personId, cancellationToken).ConfigureAwait(false) switch
        {
            true => EntryStatus.Admitted,
            false => EntryStatus.Deactivated,
            null => EntryStatus.Unknown,
        };

    /// <summary>The identifier of the person who goes by the name of use, active or not; null when nobody does. It does not authenticate.</summary>
    /// <param name="name">The name of use the person gave to enter.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<long?> IdOfAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        return FindAsync(name, cancellationToken);
    }

    private async Task<long?> FindAsync(string name, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(PersonStatements.IdOfName);
        await using (command.ConfigureAwait(false))
        {
            Add(command, "name", NpgsqlDbType.Text, name);
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as long?;
        }
    }

    /// <summary>What a person reaches. Concessions are not recorded yet, so everyone reaches nothing, and an empty reach is no error.</summary>
    /// <param name="personId">The person whose reach is asked.</param>
    public static PersonReach ReachOf(long personId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(personId);
        return PersonReach.None;
    }

    private static void Add(NpgsqlCommand command, string name, NpgsqlDbType type, object? value) =>
        PersonQueries.Add(command, name, type, value);

    private async Task<PersonResult> WhyNotDeactivatedAsync(long personId, CancellationToken cancellationToken) =>
        await dataSource.ReadActiveAsync(personId, cancellationToken).ConfigureAwait(false) is null ? PersonRefusals.NotFound() : PersonRefusals.Deactivated();

    private async Task<PersonResult> WhyNotKeptAsync(long personId, CancellationToken cancellationToken) =>
        await dataSource.ReadActiveAsync(personId, cancellationToken).ConfigureAwait(false) switch
        {
            null => PersonRefusals.NotFound(),
            false => PersonRefusals.Deactivated(),
            true => PersonRefusals.LastAdministrator(),
        };

    private async Task<PersonResult> WhyNotChangedAsync(long personId, CancellationToken cancellationToken) =>
        await dataSource.ReadActiveAsync(personId, cancellationToken).ConfigureAwait(false) switch
        {
            null => PersonRefusals.NotFound(),
            false => PersonRefusals.Deactivated(),
            true => PersonRefusals.FunctionUnchanged(),
        };
}
