using System.Globalization;
using Core.Persistence.Ingestion;

namespace Core.Host.Endpoints;

/// <summary>
/// What the list of the batches is asked for in the address: the state, the period in UTC (ISO 8601) and the page, which the server cuts.
/// The domains are never asked for here: they are the ones granted.
/// A parameter that is not accepted is named by the field, and nothing is listed.
/// </summary>
internal static class BatchQuery
{
    private const string PageSizeField = "pageSize";
    private const int DefaultPageSize = 50;
    private const int MostPageSize = 100;

    /// <summary>The filter and the page asked for, over the domains granted; null when some parameter is not accepted.</summary>
    /// <param name="query">The address's parameters.</param>
    /// <param name="granted">The domains granted.</param>
    internal static Asked? Of(IQueryCollection query, IReadOnlyCollection<int> granted)
    {
        if (Problems(query).Count > 0)
        {
            return null;
        }

        var page = Number(query["page"], 1);
        var size = Number(query[PageSizeField], DefaultPageSize);
        var filter = new BatchFilter
        {
            Domains = granted,
            State = query.TryGetValue("state", out var state) ? StateOf(state) : null,
            From = Moment(query["from"]),
            Until = Moment(query["until"]),
            Skip = (page - 1) * size,
            Take = size,
        };
        return new Asked(filter, page, size);
    }

    /// <summary>The parameters that are not accepted, by name.</summary>
    /// <param name="query">The address's parameters.</param>
    internal static IReadOnlyList<FieldShown> Problems(IQueryCollection query)
    {
        var problems = new[]
        {
            When(query.TryGetValue("state", out var state) && StateOf(state) is null, "state"),
            When(query.TryGetValue("from", out var from) && Moment(from) is null, "from"),
            When(query.TryGetValue("until", out var until) && Moment(until) is null, "until"),
            When(query.TryGetValue("page", out var page) && (Number(page, 0) < 1 || Skipped(query) > BatchRegister.MostSkipped), "page"),
            When(query.TryGetValue(PageSizeField, out var size) && Number(size, 0) is < 1 or > MostPageSize, PageSizeField),
        }.OfType<FieldShown>().ToList();
        return problems;
    }

    /// <summary>How many batches the page asked for skips, counted wide so that no page can overflow it.</summary>
    private static long Skipped(IQueryCollection query) => (long)(Number(query["page"], 1) - 1) * Math.Min(Number(query[PageSizeField], DefaultPageSize), MostPageSize);

    private static FieldShown? When(bool wrong, string field) => wrong ? new FieldShown(field, "invalid") : null;

    private static int Number(string? text, int absent)
    {
        var parsed = int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
        return string.IsNullOrEmpty(text) ? absent : parsed;
    }

    /// <summary>The state by its name and no other way: a number, a list of names or a combination is not a state.</summary>
    private static BatchState? StateOf(string? text) => text?.ToUpperInvariant() switch
    {
        "OPEN" => BatchState.Open,
        "ACCEPTED" => BatchState.Accepted,
        "REJECTED" => BatchState.Rejected,
        _ => null,
    };

    /// <summary>The moment in ISO 8601, with the offset or the Z, or only the date, which is the start of that day in UTC; any other way of writing a moment is not accepted.</summary>
    private static DateTimeOffset? Moment(string? text) =>
        Parsed(text, "yyyy-MM-dd'T'HH:mm:ssK") ?? Parsed(text, "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK") ?? Parsed(text, "yyyy-MM-dd");

    private static DateTimeOffset? Parsed(string? text, string format) =>
        DateTimeOffset.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var moment) ? moment : null;

    /// <summary>The filter, and the page it was cut for.</summary>
    /// <param name="Filter">The domains granted, the state, the period and the page.</param>
    /// <param name="Page">The page, from one.</param>
    /// <param name="PageSize">How many batches a page holds.</param>
    internal sealed record Asked(BatchFilter Filter, int Page, int PageSize);
}
