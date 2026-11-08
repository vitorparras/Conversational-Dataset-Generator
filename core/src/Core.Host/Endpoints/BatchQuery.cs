using System.Globalization;
using Core.Persistence.Ingestion;

namespace Core.Host.Endpoints;

/// <summary>
/// What the list of the batches is asked for in the address: the state, the period in UTC (ISO 8601) and the page, which the server cuts. The domains are never asked for here: they are the ones granted.
/// A parameter that is not accepted is named by the field, and nothing is listed.
/// </summary>
internal static class BatchQuery
{
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
        var size = Math.Min(Number(query["pageSize"], DefaultPageSize), MostPageSize);
        var filter = new BatchFilter
        {
            Domains = granted,
            State = query.TryGetValue("state", out var state) ? Enum.Parse<BatchState>(state.ToString(), ignoreCase: true) : null,
            From = Moment(query["from"]),
            Until = Moment(query["until"]),
            Skip = (page - 1) * size,
            Take = size,
        };
        return new Asked(filter, page, size);
    }

    /// <summary>The parameters that are not accepted, by name.</summary>
    /// <param name="query">The address's parameters.</param>
    internal static List<FieldShown> Problems(IQueryCollection query)
    {
        var problems = new List<FieldShown>();
        AddWhen(problems, query.TryGetValue("state", out var state) && !Enum.TryParse<BatchState>(state.ToString(), ignoreCase: true, out _), "state");
        AddWhen(problems, query.TryGetValue("from", out var from) && Moment(from) is null, "from");
        AddWhen(problems, query.TryGetValue("until", out var until) && Moment(until) is null, "until");
        AddWhen(problems, query.TryGetValue("page", out var page) && Number(page, 0) < 1, "page");
        AddWhen(problems, query.TryGetValue("pageSize", out var size) && Number(size, 0) < 1, "pageSize");
        return problems;
    }

    private static void AddWhen(List<FieldShown> problems, bool wrong, string field)
    {
        if (wrong)
        {
            problems.Add(new FieldShown(field, "invalid"));
        }
    }

    private static int Number(string? text, int absent) =>
        string.IsNullOrEmpty(text) ? absent : int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;

    private static DateTimeOffset? Moment(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var moment) ? moment : null;

    /// <summary>The filter, and the page it was cut for.</summary>
    /// <param name="Filter">The domains granted, the state, the period and the page.</param>
    /// <param name="Page">The page, from one.</param>
    /// <param name="PageSize">How many batches a page holds.</param>
    internal sealed record Asked(BatchFilter Filter, int Page, int PageSize);
}
