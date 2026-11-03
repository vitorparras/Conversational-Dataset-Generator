using Core.Persistence.ServerGuarantees;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// Lists what the database server's audit log says it saw in an interval. A route of a live session; that the session is of a person with the
/// Auditoria function is judged in phase 6, which is where the sessions come to carry a function.
/// </summary>
internal static class AuditEndpoint
{
    internal const string Route = "/audit/server";

    /// <summary>The widest interval one request takes, and the most lines one answer carries: a wider one is asked again in parts.</summary>
    internal static readonly TimeSpan MaxSpan = TimeSpan.FromDays(7);

    internal const int MaxEntries = 10_000;

    private const string Accepted =
        "from e to são instantes com fuso, como 2026-11-01T12:00:00Z, from vem antes de to e o intervalo não passa de 7 dias";

    private const string TooMany = "o intervalo tem mais de 10000 linhas de auditoria: peça um intervalo menor, de no máximo 7 dias";

    internal static void Map(IEndpointRouteBuilder endpoints) => _ = endpoints.MapGet(Route, ListAsync);

    /// <summary>
    /// An interval that is not two instants in order, or is wider than a week, or holds more lines than an answer carries, is a request refused
    /// with what is accepted; a log the host cannot read answers 503, since what failed is the log and not the request, and is written to the host's log
    /// with the file and the type of the failure.
    /// </summary>
    private static async Task<IResult> ListAsync(
        [AsParameters] Asked asked, [FromServices] ServerAuditLog log, [FromServices] ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        if (AuditConfiguration.Instant(asked.From) is not { } start || AuditConfiguration.Instant(asked.To) is not { } end || start >= end || end - start > MaxSpan)
        {
            return Refused(Accepted);
        }

        try
        {
            var entries = await log.ReadAsync(start, end, MaxEntries + 1, cancellationToken);
            return entries.Count > MaxEntries ? Refused(TooMany) : Results.Ok(new AuditListed(start, end, entries));
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            loggers.CreateLogger(nameof(AuditEndpoint)).Unreadable(unreadable.Data[ServerAuditLog.FileKey] as string ?? "(desconhecido)", unreadable.GetType().Name);
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>The two ends of the interval as the query carries them, still text.</summary>
    private readonly record struct Asked([FromQuery] string? From, [FromQuery] string? To);

    private static IResult Refused(string accepted) => Results.Json(new AuditRefused(accepted), statusCode: StatusCodes.Status400BadRequest);
}
