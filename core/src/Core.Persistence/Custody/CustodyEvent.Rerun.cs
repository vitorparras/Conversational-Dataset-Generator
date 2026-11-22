namespace Core.Persistence.Custody;

public sealed partial record CustodyEvent
{
    /// <summary>The event of a request of the rerun of the sanitization, by the person who asked; the item is the request and the reference the version it reruns to.</summary>
    internal static CustodyEvent SanitizationRerun(long personId, long rerunId, string version) =>
        new(CustodyAuthor.Person(personId), new CustodyItem(CustodyItemKind.SanitizationRerun, rerunId), CustodyStep.SanitizationRerun, CustodyReason.AdministrativeAct)
        {
            Reference = version,
        };

    /// <summary>The event of the conclusion of the rerun of an item, by the system; the reason is the result and the reference the version the request reruns to.</summary>
    internal static CustodyEvent ItemResanitized(long itemId, CustodyReason result, string version) =>
        new(CustodyAuthor.System, new CustodyItem(CustodyItemKind.ItemText, itemId), CustodyStep.ItemResanitized, result) { Reference = version };
}
