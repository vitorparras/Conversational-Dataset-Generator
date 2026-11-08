using Core.Persistence.Custody.Verification;
using Npgsql;

namespace Core.Host.Endpoints;

/// <summary>
/// What the host says about the verification of the chain, read and asked for in one place: the route <c>/custody/verification</c> and the screen of the chain answer with what
/// <see cref="ReadAsync" /> and <see cref="VerifyAsync" /> return, so the rule of what was verified last, who asked and how often one may ask is not written twice.
/// Nothing here rebuilds a link: a verification only reads the chain and records its verdict.
/// </summary>
internal static class ChainState
{
    /// <summary>How many verifications of the history are shown.</summary>
    internal const int HistoryShown = 20;

    /// <summary>
    /// The last verification with the interval in force, whether one is late, the seal and the guarantees, and the history of the verifications with who asked for each.
    /// Null when no database is declared or the chain cannot be read now, which the log of the host tells with the type of the failure.
    /// </summary>
    internal static async Task<ChainVerificationShown?> ReadAsync(ChainSources sources, CancellationToken cancellationToken)
    {
        var (verification, declared, _, time, loggers) = sources;
        if (verification.Reader is not { } reader)
        {
            return null;
        }

        try
        {
            var last = await reader.LastAsync(cancellationToken);
            var lastSeal = verification.SealReader is { } seals ? await seals.LastAsync(cancellationToken) : null;
            var recorded = await reader.LastGuaranteesAsync(cancellationToken);
            var guarantees = GuaranteesShown.Of(recorded, verification.IsOverdue(recorded?.RecordedAt));
            var window = await reader.OwnerWindowAsync(cancellationToken);
            var warning = SealAge.Warning(declared.Situation, lastSeal, time.GetUtcNow());
            var protection = SealProtection.Of(declared.Situation, lastSeal);
            var history = await reader.HistoryAsync(HistoryShown, cancellationToken);
            var overdue = verification.IsOverdue(last?.RecordedAt);
            return new ChainVerificationShown(last, verification.Schedule.Interval, overdue, declared.Situation.ToString(), protection, lastSeal, warning, guarantees, window, history);
        }
        catch (Exception unreadable) when (unreadable is NpgsqlException or InvalidOperationException or TimeoutException)
        {
            loggers.CreateLogger(nameof(ChainState)).LastUnreadable(unreadable.GetType().Name);
            return null;
        }
    }

    /// <summary>
    /// Verifies the chain for the session given, against the seal when one is brought, and records the verdict with who asked. A session already within the window of
    /// <see cref="VerificationLimit" /> is refused before the chain is read, with the time to wait. Neither a limit nor a failure is told apart from "not done" by anything but the answer.
    /// </summary>
    internal static async Task<AskedVerification> VerifyAsync(ChainSources sources, string? session, ChainSeal? seal, CancellationToken cancellationToken)
    {
        var (verification, _, sessions, time, _) = sources;
        if (verification.Limit.Admit(session ?? string.Empty, time.GetUtcNow()) is { } left)
        {
            return new AskedVerification(null, (int)Math.Ceiling(left.TotalSeconds));
        }

        var done = await verification.VerifyAsync(seal, sessions.Resolve(session), cancellationToken);
        return new AskedVerification(done, null);
    }
}
