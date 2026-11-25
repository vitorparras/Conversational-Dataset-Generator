using System.Globalization;
using System.Text.RegularExpressions;

namespace Core.Ingestion.Contracts.Reversal;

/// <summary>
/// Whether a request to reverse a marker may be asked for (requirement 007): it names one marker, in one declared item, and says why the analysis does not advance without the
/// identity, what will be done with it and for how many days. A request without the four is incomplete and asks nothing. One that reaches more than one item, more than one
/// marker, or no item, which is the marker wherever it is in the domain, is the reidentification of the collection, and is refused by its scope.
/// </summary>
public static partial class ReversalRequestRule
{
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
        var missing = new List<ReversalRequestField>();
        Note(missing, ReversalRequestField.Item, Array.TrueForAll(items, IsItem));
        Note(missing, ReversalRequestField.Marker, markers.Length > 0 && Array.TrueForAll(markers, MarkerShape().IsMatch));
        Note(missing, ReversalRequestField.Term, term > 0);
        Note(missing, ReversalRequestField.Reason, !string.IsNullOrWhiteSpace(draft.Reason));
        Note(missing, ReversalRequestField.Use, !string.IsNullOrWhiteSpace(draft.Use));
        return missing;
    }

    private static void Note(List<ReversalRequestField> missing, ReversalRequestField field, bool held)
    {
        if (!held)
        {
            missing.Add(field);
        }
    }

    private static ReversalScope? ScopeOf(int items, int markers) => (items, markers) switch
    {
        (0, _) => ReversalScope.Domain,
        ( > 1, _) => ReversalScope.ManyItems,
        (_, > 1) => ReversalScope.ManyMarkers,
        _ => null,
    };

    private static bool IsItem(string part) => long.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0;

    private static string[] Parts(string? field) => (field ?? string.Empty).Split(Separators, StringSplitOptions.RemoveEmptyEntries);

    [GeneratedRegex("^(PESSOA|LUGAR|ORGANIZACAO)_[0-9]{8}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MarkerShape();
}
