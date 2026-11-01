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
    public string Open(long personId)
    {
        var now = clock.GetUtcNow();
        var identifier = Convert.ToHexString(RandomNumberGenerator.GetBytes(IdentifierBytes));
        lock (gate)
        {
            Forget(now);
            sessions[identifier] = new Entry(personId, now, now);
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

    private bool Passed(Entry entry, DateTimeOffset now) => now - entry.LastActivity > lifetimes.Idle || now - entry.Start > lifetimes.Absolute;

    private void Forget(DateTimeOffset now)
    {
        foreach (var identifier in sessions.Where(session => Passed(session.Value, now)).Select(session => session.Key).ToList())
        {
            _ = sessions.Remove(identifier);
        }
    }

    private sealed record Entry(long PersonId, DateTimeOffset Start, DateTimeOffset LastActivity);
}
