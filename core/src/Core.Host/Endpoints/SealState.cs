using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Npgsql;

namespace Core.Host.Endpoints;

/// <summary>
/// What the route of the seal and, next, the screen of the seal share (#736): whether the seal of the manual situation can be issued and recorded now, the seal as the chain stands and the record of
/// the one the person kept, as an act of the person of the session. Nothing here writes a path of its own: a seal is recorded only by <see cref="ChainSealer" />.
/// </summary>
internal static class SealState
{
    /// <summary>Whether the seal can be issued and recorded by hand now: only when the installation declared Manual, and with a database to read.</summary>
    internal static SealAvailability Availability(ChainSources sources)
    {
        if (sources.Declared.Situation != SealSituation.Manual)
        {
            return SealAvailability.NotManual;
        }

        return sources.Verification.Sealer is null ? SealAvailability.Unreadable : SealAvailability.Open;
    }

    /// <summary>The seal of the chain as it stands, writing nothing; null when the chain cannot be read now.</summary>
    internal static async Task<ChainSeal?> IssueAsync(ChainSources sources, CancellationToken cancellationToken)
    {
        try
        {
            return await sources.Verification.Sealer!.MakeAsync(cancellationToken);
        }
        catch (Exception unreadable) when (unreadable is NpgsqlException or InvalidOperationException or TimeoutException)
        {
            Unavailable(sources.Loggers, unreadable);
            return null;
        }
    }

    /// <summary>
    /// Records the seal the text carries in the name of the person of the session, only if the chain has that event with that digest. A text that is not a seal, a seal the chain does not
    /// have and a session that ended meanwhile are told apart from a chain that cannot be read, and in none of them is anything written.
    /// </summary>
    /// <param name="sources">The services of the host.</param>
    /// <param name="session">The identifier of the session, as the cookie carries it.</param>
    /// <param name="text">The text of the seal, as the person kept it.</param>
    /// <param name="cancellationToken">Cancels the record.</param>
    internal static async Task<SealConfirmation> ConfirmAsync(ChainSources sources, string? session, string? text, CancellationToken cancellationToken)
    {
        var reading = ChainSeal.Read(text);
        if (reading.Seal is not { } seal)
        {
            return SealConfirmation.Refused(SealConfirmationOutcome.NotASeal, reading.Problem.ToString()!, reading.Refusal!);
        }

        if (sources.Sessions.Resolve(session) is not { } person)
        {
            return SealConfirmation.Refused(SealConfirmationOutcome.NoSession, string.Empty, string.Empty);
        }

        try
        {
            var recording = await sources.Verification.Sealer!.RecordAsync(seal, CustodyAuthor.Person(person), cancellationToken);
            if (recording.Seal is null)
            {
                return SealConfirmation.Refused(SealConfirmationOutcome.NotRecorded, recording.Recorded.Outcome.ToString(), recording.Recorded.Refusal!);
            }

            await sources.Verification.RefreshSealWarningAsync(cancellationToken);
            return SealConfirmation.Done(new SealConfirmed(recording.Recorded.Sequence!.Value, seal.Events, seal.SealedAt));
        }
        catch (Exception unreadable) when (unreadable is NpgsqlException or InvalidOperationException or TimeoutException)
        {
            Unavailable(sources.Loggers, unreadable);
            return SealConfirmation.Refused(SealConfirmationOutcome.Unreadable, string.Empty, string.Empty);
        }
    }

    private static void Unavailable(ILoggerFactory loggers, Exception failure) => loggers.CreateLogger(nameof(SealState)).SealRouteUnavailable(failure.GetType().Name);
}
