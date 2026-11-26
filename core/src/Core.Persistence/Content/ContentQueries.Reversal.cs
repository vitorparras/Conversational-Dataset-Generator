namespace Core.Persistence.Content;

/// <summary>The statements of the request to reverse a marker (fatia C): rows that are only added, and where a request stands, read from its rows and its events at the moment given.</summary>
public static partial class ContentQueries
{
    /// <summary>
    /// The requests with where each stands at the moment given and how many the reading leaves, which a condition narrows; the last authorization says whether the request is
    /// authorized, expired or revealed, unless the request was declined.
    /// </summary>
    internal const string ReadReversalStanding =
        "SELECT s.id, s.item_id, s.marker, s.term_days, s.requested_by, s.requested_at, s.authorized_by, s.valid_until, s.state, s.authorization_id, s.revealed_at, count(*) OVER () "
        + "FROM (SELECT r.id, r.item_id, r.marker, r.term_days, r.requested_by, r.requested_at, a.authorized_by, a.valid_until, a.id AS authorization_id, a.revealed_at, "
        + "CASE WHEN EXISTS (SELECT 1 FROM public.custody_event e WHERE e.item_kind = 'reversal_request' AND e.item_id = r.id AND e.step = 'reversal_declined') THEN 'declined' "
        + "WHEN a.revealed_at IS NOT NULL THEN 'revealed' WHEN a.valid_until IS NULL THEN 'pending' WHEN a.valid_until > @now THEN 'authorized' ELSE 'expired' END AS state "
        + "FROM content.reversal_request r LEFT JOIN LATERAL (SELECT z.id, z.authorized_by, z.valid_until, v.revealed_at FROM content.reversal_authorization z "
        + "LEFT JOIN content.reversal_revelation v ON v.authorization_id = z.id WHERE z.request_id = r.id ORDER BY z.id DESC LIMIT 1) a ON true) s";

    /// <summary>The marker among those of the item; no row when it is not.</summary>
    internal const string ReadReversalMarkerInItem = "SELECT 1 FROM content.item_marker WHERE item_id = @item AND marker = @marker";

    /// <summary>A request, with where it stands.</summary>
    internal const string ReadReversalRequest = ReadReversalStanding + " WHERE s.id = @id";

    /// <summary>A page of the requests of the person, or of the others' that wait for a decision, narrowed by item and by state, with how many the list leaves.</summary>
    internal const string ReadReversalRequests =
        ReadReversalStanding + " WHERE CASE WHEN @mine THEN s.requested_by = @person ELSE s.requested_by <> @person AND s.state IN ('pending', 'expired') END "
        + "AND (@item::bigint IS NULL OR s.item_id = @item) AND (@state::text IS NULL OR s.state = lower(@state)) ORDER BY s.id DESC LIMIT @size OFFSET @skip";

    /// <summary>Why the request asks and what for, sealed, as a reading of one row selects it.</summary>
    internal const string ReadReversalRequestText = "SELECT justification, false, dek_epoch, dek_wrapped FROM content.reversal_request WHERE id = @id";

    /// <summary>Writes a request; the database lets it through only with its event in the same transaction.</summary>
    internal const string InsertReversalRequest =
        "INSERT INTO content.reversal_request (domain_id, item_id, marker, requested_by, term_days, justification, dek_epoch, dek_wrapped) "
        + "VALUES (@domain, @item, @marker, @person, @term, @justification, @epoch, @wrapped) RETURNING id";

    /// <summary>The keys, wrapped, that an epoch older than the newest wraps, among the requests; a page of them.</summary>
    internal const string ReadOlderRequestKeys = "SELECT id, dek_epoch, dek_wrapped FROM content.reversal_request WHERE dek_epoch < @newest ORDER BY id LIMIT 500";

    internal const string ReadRequestKeysAfter = "SELECT id, dek_epoch, dek_wrapped FROM content.reversal_request WHERE id > @after ORDER BY id LIMIT @batch";

    /// <summary>Wraps again the key of a request; only the wrapped key and its epoch change.</summary>
    internal const string RewrapRequestKey = "UPDATE content.reversal_request SET dek_epoch = @epoch, dek_wrapped = @wrapped WHERE id = @id";

    /// <summary>Writes an authorization; the database refuses one by who asked, one not to come and a second in force, and lets it through only with its event.</summary>
    internal const string InsertReversalAuthorization =
        "INSERT INTO content.reversal_authorization (domain_id, request_id, authorized_by, valid_until) VALUES (@domain, @request, @person, @until) RETURNING id";
}
