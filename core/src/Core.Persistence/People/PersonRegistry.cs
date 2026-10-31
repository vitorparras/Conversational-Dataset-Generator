using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.People;

/// <summary>
/// The people who operate the system. Each operation writes the person and the record of what was done in one command, so that the history
/// never lacks a change nor holds one that did not happen. A person is deactivated and never deleted; what they reach comes from grants.
/// </summary>
public sealed class PersonRegistry
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
    /// <param name="actorId">The person who acts, or null when nobody acts as a person.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public Task<PersonResult> CreateAsync(NewPerson person, long? actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(person);
        return AddAsync(person, actorId, cancellationToken);
    }

    private async Task<PersonResult> AddAsync(NewPerson person, long? actorId, CancellationToken cancellationToken)
    {
        if (PersonRefusals.Judge(person) is { } refusal)
        {
            return refusal;
        }

        try
        {
            var created = await dataSource.WriteAsync(
                PersonStatements.Create,
                command =>
                {
                    Add(command, "name", NpgsqlDbType.Text, person.Name);
                    Add(command, "function", NpgsqlDbType.Text, person.Function);
                    Add(command, "actor", NpgsqlDbType.Bigint, actorId);
                },
                cancellationToken).ConfigureAwait(false);
            return created is { } id ? PersonResult.Done(id) : await dataSource.ActorRefusalAsync(actorId, cancellationToken).ConfigureAwait(false) ?? PersonRefusals.ActorDeactivated();
        }
        catch (PostgresException refused) when (refused.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return PersonRefusals.NameTaken();
        }
    }

    /// <summary>Deactivates a person; the person stays in the registry and in the history.</summary>
    /// <param name="personId">The person to deactivate.</param>
    /// <param name="actorId">The person who acts, or null when nobody acts as a person.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public async Task<PersonResult> DeactivateAsync(long personId, long? actorId, CancellationToken cancellationToken)
    {
        var changed = await dataSource.WriteAsync(
            PersonStatements.Deactivate,
            command =>
            {
                Add(command, "id", NpgsqlDbType.Bigint, personId);
                Add(command, "actor", NpgsqlDbType.Bigint, actorId);
            },
            cancellationToken).ConfigureAwait(false);
        return changed is { } id ? PersonResult.Done(id) : await WhyNotDeactivatedAsync(personId, actorId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gives an active person another function, keeping the previous one in the history; a function outside the policy writes nothing.</summary>
    /// <param name="personId">The person whose function changes.</param>
    /// <param name="function">One of <see cref="PersonFunctions.Accepted" />.</param>
    /// <param name="actorId">The person who acts, or null when nobody acts as a person.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public Task<PersonResult> ChangeFunctionAsync(long personId, string function, long? actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(function);
        return ReplaceFunctionAsync(personId, function, actorId, cancellationToken);
    }

    private async Task<PersonResult> ReplaceFunctionAsync(long personId, string function, long? actorId, CancellationToken cancellationToken)
    {
        if (PersonRefusals.NotAccepted(function) is { } refusal)
        {
            return refusal;
        }

        var changed = await dataSource.WriteAsync(
            PersonStatements.ChangeFunction,
            command =>
            {
                Add(command, "id", NpgsqlDbType.Bigint, personId);
                Add(command, "function", NpgsqlDbType.Text, function);
                Add(command, "actor", NpgsqlDbType.Bigint, actorId);
            },
            cancellationToken).ConfigureAwait(false);
        return changed is { } id ? PersonResult.Done(id) : await WhyNotChangedAsync(personId, actorId, cancellationToken).ConfigureAwait(false);
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

    /// <summary>What a person reaches. Concessions are not recorded yet, so everyone reaches nothing, and an empty reach is no error.</summary>
    /// <param name="personId">The person whose reach is asked.</param>
    public static PersonReach ReachOf(long personId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(personId);
        return PersonReach.None;
    }

    private static void Add(NpgsqlCommand command, string name, NpgsqlDbType type, object? value) =>
        PersonQueries.Add(command, name, type, value);

    private async Task<PersonResult> WhyNotDeactivatedAsync(long personId, long? actorId, CancellationToken cancellationToken) =>
        await dataSource.ActorRefusalAsync(actorId, cancellationToken).ConfigureAwait(false)
        ?? (await dataSource.ReadActiveAsync(personId, cancellationToken).ConfigureAwait(false) is null ? PersonRefusals.NotFound() : PersonRefusals.Deactivated());

    private async Task<PersonResult> WhyNotChangedAsync(long personId, long? actorId, CancellationToken cancellationToken) =>
        await dataSource.ActorRefusalAsync(actorId, cancellationToken).ConfigureAwait(false)
        ?? await dataSource.ReadActiveAsync(personId, cancellationToken).ConfigureAwait(false) switch
        {
            null => PersonRefusals.NotFound(),
            false => PersonRefusals.Deactivated(),
            true => PersonRefusals.FunctionUnchanged(),
        };
}
