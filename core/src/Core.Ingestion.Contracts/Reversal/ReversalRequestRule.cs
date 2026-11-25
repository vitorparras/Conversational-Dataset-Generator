using System.Globalization;

namespace Core.Ingestion.Contracts.Reversal;

/// <summary>
/// Whether a request to reverse a marker may be asked for (requirement 007): it names one marker, in one declared item, and says why the analysis does not advance without the
/// identity, what will be done with it and for how many days. A request without the four is incomplete and asks nothing. One that reaches more than one item or marker, no
/// item, which is the marker wherever it is in the domain, or no marker, which is the domain as well, is the reidentification of the collection: refused by its scope.
/// </summary>
public static class ReversalRequestRule
{
    private const int Digits = 8;
    private static readonly string[] Kinds = ["PESSOA_", "LUGAR_", "ORGANIZACAO_"];
    private static readonly char[] Separators = [',', ';', ' ', '\t', '\r', '\n'];

    /// <summary>The judgment of the request as the form sent it.</summary>
    /// <param name="draft">The fields as written.</param>
    public static ReversalRequestJudgment Judge(ReversalRequestDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var items = Parts(draft.Item);
        var markers = Parts(draft.Marker);
        var term = int.TryParse(draft.Term?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var days) ? days : 0;
        var missing = Missing(items, markers, term, draft);
        if (missing.Count > 0)
        {
            return new ReversalRequestJudgment { Verdict = ReversalRequestVerdict.Incomplete, Missing = missing };
        }
        if (ScopeOf(items.Length, markers.Length) is { } scope)
        {
            return new ReversalRequestJudgment { Verdict = ReversalRequestVerdict.TooWide, Scope = scope };
        }
        var request = new ReversalRequest
        {
            ItemId = long.Parse(items[0], NumberStyles.None, CultureInfo.InvariantCulture),
            Marker = markers[0],
            TermDays = term,
            Reason = draft.Reason!.Trim(),
            Use = draft.Use!.Trim(),
        };
        return new ReversalRequestJudgment { Verdict = ReversalRequestVerdict.Accepted, Request = request };
    }

    private static List<ReversalRequestField> Missing(string[] items, string[] markers, int term, ReversalRequestDraft draft)
    {
        (ReversalRequestField Field, bool Held)[] fields =
        [
            (ReversalRequestField.Item, Array.TrueForAll(items, IsItem)),
            (ReversalRequestField.Marker, Array.TrueForAll(markers, IsMarker)),
            (ReversalRequestField.Term, term > 0),
            (ReversalRequestField.Reason, !string.IsNullOrWhiteSpace(draft.Reason)),
            (ReversalRequestField.Use, !string.IsNullOrWhiteSpace(draft.Use)),
        ];
        return [.. fields.Where(field => !field.Held).Select(field => field.Field)];
    }

    private static ReversalScope? ScopeOf(int items, int markers) => (items, markers) switch
    {
        (0, _) or (_, 0) => ReversalScope.Domain,
        ( > 1, _) => ReversalScope.ManyItems,
        (_, > 1) => ReversalScope.ManyMarkers,
        _ => null,
    };

    private static bool IsItem(string part) => long.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0;

    /// <summary>A marker is its kind and eight digits: PESSOA_, LUGAR_ or ORGANIZACAO_ and then 00000001, say.</summary>
    private static bool IsMarker(string part) => Array.Exists(
        Kinds, kind => part.Length == kind.Length + Digits && part.StartsWith(kind, StringComparison.Ordinal) && !part.AsSpan(kind.Length).ContainsAnyExceptInRange('0', '9'));

    private static string[] Parts(string? field) => (field ?? string.Empty).Split(Separators, StringSplitOptions.RemoveEmptyEntries);
}
