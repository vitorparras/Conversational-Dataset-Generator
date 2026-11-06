using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Platform.Sessions;

namespace Core.Host.Operators;

/// <summary>
/// What the people of the registry may do through the routes of a session: make a person with a password, deactivate one and set a password again. Whoever acts is the person of the session,
/// and only a person who holds the administration function acts; the function is read from the registry at each request, so that a person deactivated meanwhile no longer does.
/// </summary>
internal sealed class OperatorAdministration
{
    private readonly SessionStore sessions;

    /// <summary>The administration works over the registry and the store of passwords the entry also reads, and ends sessions in the store given.</summary>
    internal OperatorAdministration(PersonRegistry registry, PersonCredentials credentials, SessionStore sessions)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(sessions);
        Registry = registry;
        Credentials = credentials;
        this.sessions = sessions;
    }

    /// <summary>The registry the entry also reads.</summary>
    internal PersonRegistry Registry { get; }

    /// <summary>The store of passwords the entry also reads.</summary>
    internal PersonCredentials Credentials { get; }

    /// <summary>Whether the person is active and holds the administration function.</summary>
    internal async Task<bool> IsAdministratorAsync(long subject, CancellationToken cancellationToken) =>
        await Registry.ActiveFunctionOfAsync(subject, cancellationToken).ConfigureAwait(false) == PersonFunctions.Administration;

    /// <summary>Makes the person and then sets the password, both by the author; the answer of the password is the one returned when the person was made.</summary>
    internal async Task<(PersonResult Made, PersonResult? Password)> CreateAsync(long author, NewPerson fresh, string password, CancellationToken cancellationToken)
    {
        var by = CustodyAuthor.Person(author);
        var made = await Registry.CreateAsync(fresh, by, cancellationToken).ConfigureAwait(false);
        return made.Outcome != PersonOutcome.Done
            ? (made, null)
            : (made, await Credentials.SetPasswordAsync(made.PersonId!.Value, password, by, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deactivates the person, never the last administrator, and ends the sessions they had open.</summary>
    internal async Task<PersonResult> DeactivateAsync(long author, long subject, CancellationToken cancellationToken)
    {
        var result = await Registry.DeactivateKeepingAdministrationAsync(subject, CustodyAuthor.Person(author), cancellationToken).ConfigureAwait(false);
        if (result.Outcome == PersonOutcome.Done)
        {
            sessions.CloseOf(subject);
        }

        return result;
    }

    /// <summary>Sets the password of the person again, by the author, and ends the sessions they had open, which were made with the password that no longer opens.</summary>
    internal async Task<PersonResult> SetPasswordAsync(long author, long subject, string password, CancellationToken cancellationToken)
    {
        var result = await Credentials.SetPasswordAsync(subject, password, CustodyAuthor.Person(author), cancellationToken).ConfigureAwait(false);
        if (result.Outcome == PersonOutcome.Done)
        {
            sessions.CloseOf(subject);
        }

        return result;
    }

    /// <summary>One page of the people the filter leaves, by name, with the total; it reads the name, the function and the state, nothing of any material.</summary>
    internal Task<PersonPage> ListAsync(PersonFilter filter, int page, CancellationToken cancellationToken) => Registry.ListAsync(filter, page, cancellationToken);
}
