using System.Security.Cryptography;

namespace Core.Platform.Sessions;

/// <summary>
/// The sessions of the people who entered, kept in memory and nowhere else: a restart ends them all. Closing a session, or letting it pass
/// either limit, removes it here, so an identifier presented afterwards is refused because the store does not know it, whatever the client kept.
/// The identifier is 256 random bits and is never written to a log or an exception.
/// </summary>
public sealed class SessionStore(SessionLifetimes lifetimes, TimeProvider clock)
{
    private const int IdentifierBytes = 32;

    private readonly Lock gate = new();
    private readonly Dictionary<string, Entry> sessions = new(StringComparer.Ordinal);

    /// <summary>Starts a session for the person and returns the identifier that names it.</summary>
    public string Open(long personId) => Open(personId, SessionDevice.Unknown);

    /// <summary>Starts a session for the person, keeping with it only the short description of the device; it goes when the session does.</summary>
    public string Open(long personId, string device)
    {
        var now = clock.GetUtcNow();
        var identifier = NewIdentifier();
        lock (gate)
        {
            Forget(now);
            sessions[identifier] = new Entry(personId, now, now, NewIdentifier(), device);
        }

        return identifier;
    }

    /// <summary>The person the session belongs to, with its inactivity limit started again; null when the identifier is unknown, was closed or passed a limit, which also removes it.</summary>
    public long? Resolve(string? identifier)
    {
        if (identifier is null)
        {
            return null;
        }

        var now = clock.GetUtcNow();
        lock (gate)
        {
            if (!sessions.TryGetValue(identifier, out var entry))
            {
                return null;
            }

            if (Passed(entry, now))
            {
                _ = sessions.Remove(identifier);
                return null;
            }

            sessions[identifier] = entry with { LastActivity = now };
            return entry.PersonId;
        }
    }

    /// <summary>Ends the session; an identifier the store does not know is left alone.</summary>
    public void Close(string? identifier)
    {
        if (identifier is null)
        {
            return;
        }

        lock (gate)
        {
            _ = sessions.Remove(identifier);
        }
    }

    /// <summary>Ends every session of the person, so that an identifier they hold is refused from then on; the person may have none. Returns how many it ended.</summary>
    public int CloseOf(long personId)
    {
        lock (gate)
        {
            var open = sessions.Where(session => session.Value.PersonId == personId).Select(session => session.Key).ToList();
            foreach (var identifier in open)
            {
                _ = sessions.Remove(identifier);
            }

            return open.Count;
        }
    }

    /// <summary>
    /// The sessions of the person that are still open, the oldest first, with the one that asked marked as current. Reading them does not use them: no inactivity limit starts again.
    /// Only the listing identifier of each leaves, never the identifier of the session.
    /// </summary>
    public IReadOnlyList<SessionListing> ListOf(long personId, string? currentIdentifier)
    {
        var now = clock.GetUtcNow();
        lock (gate)
        {
            Forget(now);
            var listed = sessions
                .Where(session => session.Value.PersonId == personId)
                .OrderBy(session => session.Value.Start)
                .Select(session => new SessionListing(
                    session.Value.ListingId,
                    session.Value.Start,
                    session.Value.LastActivity,
                    string.Equals(session.Key, currentIdentifier, StringComparison.Ordinal),
                    session.Value.Device))
                .ToList();
            return listed;
        }
    }

    /// <summary>
    /// Ends the session of the person that the listing identifier names, so that it is refused from then on; false when the person has none by that identifier,
    /// which is also the answer for the identifier of another person's session.
    /// </summary>
    public bool CloseListed(long personId, string? listingId)
    {
        if (listingId is null)
        {
            return false;
        }

        lock (gate)
        {
            var found = sessions.Where(session => session.Value.PersonId == personId && string.Equals(session.Value.ListingId, listingId, StringComparison.Ordinal)).ToList();
            foreach (var session in found)
            {
                _ = sessions.Remove(session.Key);
            }

            return found.Count > 0;
        }
    }

    /// <summary>Ends every session of the person but the one given, and says how many it ended; the one given stays even if the store does not know it.</summary>
    public int CloseOthersOf(long personId, string? keptIdentifier)
    {
        lock (gate)
        {
            var others = sessions
                .Where(session => session.Value.PersonId == personId && !string.Equals(session.Key, keptIdentifier, StringComparison.Ordinal))
                .Select(session => session.Key)
                .ToList();
            foreach (var identifier in others)
            {
                _ = sessions.Remove(identifier);
            }

            return others.Count;
        }
    }

    private static string NewIdentifier() => Convert.ToHexString(RandomNumberGenerator.GetBytes(IdentifierBytes));

    private bool Passed(Entry entry, DateTimeOffset now) => now - entry.LastActivity > lifetimes.Idle || now - entry.Start > lifetimes.Absolute;

    private void Forget(DateTimeOffset now)
    {
        foreach (var identifier in sessions.Where(session => Passed(session.Value, now)).Select(session => session.Key).ToList())
        {
            _ = sessions.Remove(identifier);
        }
    }

    private sealed record Entry(long PersonId, DateTimeOffset Start, DateTimeOffset LastActivity, string ListingId, string Device);
}
