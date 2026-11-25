using System.Globalization;
using Core.Ingestion.Contracts.Reversal;

namespace Core.Persistence.Custody;

public sealed partial record CustodyEvent
{
    /// <summary>The event of a request to reverse a marker, by who asked; the item is the request and the reference the marker. Why and for what never go in the event.</summary>
    internal static CustodyEvent ReversalRequested(long personId, long requestId, string marker) =>
        new(CustodyAuthor.Person(personId), new CustodyItem(CustodyItemKind.ReversalRequest, requestId), CustodyStep.ReversalRequested, CustodyReason.AdministrativeAct)
        {
            Reference = marker,
        };

    /// <summary>
    /// The refusal of a request to reverse one marker in one item, by who asked; the item is the item and the reference the marker. The reason is
    /// <see cref="CustodyReason.ReversalFunction"/> when who asked holds no function of the curation, <see cref="CustodyReason.ReversalScope"/> when the item holds no such marker.
    /// </summary>
    internal static CustodyEvent ReversalRequestRefused(long personId, long itemId, string marker, CustodyReason reason) =>
        new(CustodyAuthor.Person(personId), new CustodyItem(CustodyItemKind.ItemText, itemId), CustodyStep.ReversalRequestRefused, reason)
        {
            Reference = marker,
        };

    /// <summary>The refusal of a request that reached more than one marker in one item, by who asked; the item is the domain and the reference names the scope, never a marker.</summary>
    internal static CustodyEvent ReversalRequestRefusedByScope(long personId, int domainId, ReversalScope scope) =>
        new(CustodyAuthor.Person(personId), new CustodyItem(CustodyItemKind.Domain, domainId), CustodyStep.ReversalRequestRefused, CustodyReason.ReversalScope)
        {
            Reference = scope switch
            {
                ReversalScope.Domain => "dominio",
                ReversalScope.ManyItems => "varios-itens",
                ReversalScope.ManyMarkers => "varios-marcadores",
                _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "The scope of a refusal is the domain, many items or many markers."),
            },
        };

    /// <summary>The event of an authorization, by who authorized; the item is the authorization and the reference the identifier of the request.</summary>
    internal static CustodyEvent ReversalAuthorized(long personId, long authorizationId, long requestId) =>
        new(CustodyAuthor.Person(personId), new CustodyItem(CustodyItemKind.ReversalAuthorization, authorizationId), CustodyStep.ReversalAuthorized, CustodyReason.AdministrativeAct)
        {
            Reference = requestId.ToString(CultureInfo.InvariantCulture),
        };

    /// <summary>The refusal of an authorization, by who meant to authorize; the reason is <see cref="CustodyReason.ReversalSelf"/> or <see cref="CustodyReason.ReversalFunction"/>.</summary>
    internal static CustodyEvent ReversalAuthorizationRefused(long personId, long requestId, CustodyReason reason) =>
        new(CustodyAuthor.Person(personId), new CustodyItem(CustodyItemKind.ReversalRequest, requestId), CustodyStep.ReversalAuthorizationRefused, reason);

    /// <summary>The decline of a request, by who declined it; the request is never authorized after.</summary>
    internal static CustodyEvent ReversalDeclined(long personId, long requestId) =>
        new(CustodyAuthor.Person(personId), new CustodyItem(CustodyItemKind.ReversalRequest, requestId), CustodyStep.ReversalDeclined, CustodyReason.AdministrativeAct);
}
