using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Platform.Sessions;

namespace Core.Host.People;

/// <summary>
/// What the people of the registry may do through the routes of a session: make a person with a password, deactivate one and set a password again. Whoever acts is the person of the session,
/// and only a person who holds the administration function acts; the function is read from the registry at each request, so that a person deactivated meanwhile no longer does.
/// </summary>
internal sealed class PeopleAdministration(PersonRegistry registry, PersonCredentials credentials, SessionStore sessions)
{
    /// <summary>The registry the entry also reads.</summary>
    internal PersonRegistry Registry { get; } = registry;

    /// <summary>The store of passwords the entry also reads.</summary>
    internal PersonCredentials Credentials { get; } = credentials;

    /// <summary>Whether the person is active and holds the administration function.</summary>
    internal async Task<bool> IsAdministratorAsync(long person, CancellationToken cancellationToken) =>
        await Registry.ActiveFunctionOfAsync(person, cancellationToken).ConfigureAwait(false) == PersonFunctions.Administration;

    /// <summary>Makes the person and then sets the password, both by the author; the answer of the password is the one returned when the person was made.</summary>
    internal async Task<(PersonResult Made, PersonResult? Password)> CreateAsync(long author, NewPerson person, string password, CancellationToken cancellationToken)
    {
        var by = CustodyAuthor.Person(author);
        var made = await Registry.CreateAsync(person, by, cancellationToken).ConfigureAwait(false);
        return made.Outcome != PersonOutcome.Done
            ? (made, null)
            : (made, await Credentials.SetPasswordAsync(made.PersonId!.Value, password, by, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deactivates the person, never the last administrator, and ends the sessions they had open.</summary>
    internal async Task<PersonResult> DeactivateAsync(long author, long person, CancellationToken cancellationToken)
    {
        var result = await Registry.DeactivateKeepingAdministrationAsync(person, CustodyAuthor.Person(author), cancellationToken).ConfigureAwait(false);
        if (result.Outcome == PersonOutcome.Done)
        {
            sessions.CloseOf(person);
        }

        return result;
    }

    /// <summary>Sets the password of the person again, by the author.</summary>
    internal Task<PersonResult> SetPasswordAsync(long author, long person, string password, CancellationToken cancellationToken) =>
        Credentials.SetPasswordAsync(person, password, CustodyAuthor.Person(author), cancellationToken);
}
