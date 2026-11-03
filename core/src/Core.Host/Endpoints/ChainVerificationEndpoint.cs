using Microsoft.AspNetCore.Mvc;
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
    private static async Task<IResult> ShowAsync(
        [FromServices] ChainVerificationService verification, [FromServices] TimeProvider time, [FromServices] ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        if (verification.Reader is not { } reader)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        try
        {
            var last = await reader.LastAsync(cancellationToken);
            var guarantees = await reader.LastGuaranteesAsync(cancellationToken);
            var schedule = verification.Schedule;
            var now = time.GetUtcNow();
            var shown = GuaranteesShown.Of(guarantees, schedule.Overdue(guarantees?.RecordedAt, now));
            return Results.Ok(new ChainVerificationShown(last, schedule.Interval, schedule.Overdue(last?.RecordedAt, now), shown, await reader.OwnerWindowAsync(cancellationToken)));
        }
        catch (Exception unreadable) when (unreadable is NpgsqlException or InvalidOperationException or TimeoutException)
        {
            loggers.CreateLogger(nameof(ChainVerificationEndpoint)).LastUnreadable(unreadable.GetType().Name);
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
