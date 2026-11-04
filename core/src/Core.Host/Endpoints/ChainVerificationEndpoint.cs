using Npgsql;

namespace Core.Host.Endpoints;

/// <summary>
/// Shows the last verification of the chain of custody as the chain records it, with the interval in force and whether one is late. A route of a live session and not part of the
/// status, which answers with no session: the state of the chain and the count of its events are not for whoever did not enter.
/// </summary>
internal static class ChainVerificationEndpoint
{
    internal const string Route = "/custody/verification";

    internal static void Map(IEndpointRouteBuilder endpoints) => _ = endpoints.MapGet(Route, ShowAsync);

    /// <summary>With no database declared there is no chain to read, and one that cannot be read answers 503, with the type of the failure in the host's log.</summary>
    private static async Task<IResult> ShowAsync([AsParameters] CustodySources sources, CancellationToken cancellationToken)
    {
        var (verification, declared, _, time, loggers) = sources;
        if (verification.Reader is not { } reader)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        try
        {
            var last = await reader.LastAsync(cancellationToken);
            var lastSeal = await LastSealAsync(verification, cancellationToken);
            var protection = SealProtection.Of(declared.Situation, lastSeal);
            var warning = SealAge.Warning(declared.Situation, lastSeal, time.GetUtcNow());
            return Results.Ok(new ChainVerificationShown(last, verification.Schedule.Interval, verification.IsOverdue(last?.RecordedAt), declared.Situation.ToString(), protection, lastSeal, warning));
        }
        catch (Exception unreadable) when (unreadable is NpgsqlException or InvalidOperationException or TimeoutException)
        {
            loggers.CreateLogger(nameof(ChainVerificationEndpoint)).LastUnreadable(unreadable.GetType().Name);
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>When the chain last recorded a seal, whatever the situation: the sending of the seal and the manual record both leave it there; null when it holds none.</summary>
    private static Task<DateTimeOffset?> LastSealAsync(ChainVerificationService verification, CancellationToken cancellationToken) =>
        verification.SealReader is { } reader ? reader.LastAsync(cancellationToken) : Task.FromResult<DateTimeOffset?>(null);
}
