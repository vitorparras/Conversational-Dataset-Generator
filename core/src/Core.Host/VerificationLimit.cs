namespace Core.Host;

/// <summary>
/// How often a session may ask for the verification against the seal, which reads the whole chain: one in each window, counted by the clock of the host and kept in its memory, so a restart
/// clears it. A session is told how long to wait, and a session that waits does not make another wait.
/// </summary>
internal sealed class VerificationLimit
{
    internal static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    private readonly Lock gate = new();
    private readonly Dictionary<string, DateTimeOffset> admitted = new(StringComparer.Ordinal);

    /// <summary>Admits the session when it has not been admitted within the window, and takes the moment; otherwise says how long is left of the window.</summary>
    /// <param name="session">The identifier of the session.</param>
    /// <param name="now">The moment of the request.</param>
    /// <returns>Null when the session is admitted; the time left of the window when it is not.</returns>
    internal TimeSpan? Admit(string session, DateTimeOffset now)
    {
        lock (gate)
        {
            Forget(now);
            if (admitted.TryGetValue(session, out var last))
            {
                return last + Window - now;
            }

            admitted[session] = now;
            return null;
        }
    }

    private void Forget(DateTimeOffset now)
    {
        foreach (var session in admitted.Where(entry => now - entry.Value >= Window).Select(entry => entry.Key).ToList())
        {
            _ = admitted.Remove(session);
        }
    }
}
