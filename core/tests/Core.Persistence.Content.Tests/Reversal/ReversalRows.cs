using Core.Persistence.Tests.Content;

namespace Core.Persistence.Tests.Reversal;

/// <summary>
/// Rows of a request and of an authorization written straight to the database, each with its event in the same statement, as whoever does not go by the storage would: what the
/// database itself refuses is judged by these, apart from the judgment of the application.
/// </summary>
internal static class ReversalRows
{
    /// <summary>The marker of the requests these write.</summary>
    public const string Marker = "PESSOA_48291307";

    private const string Request =
        "WITH r AS (INSERT INTO content.reversal_request (domain_id, item_id, marker, requested_by, term_days, justification, dek_epoch, dek_wrapped) "
        + "VALUES (@domain, 118, 'PESSOA_48291307', @person, 30, '\\x00'::bytea, 1, '\\x00'::bytea) RETURNING id) "
        + "INSERT INTO public.custody_event (actor_person_id, actor_system, item_kind, item_id, step, reason, reference) "
        + "SELECT @person, false, 'reversal_request', r.id, 'reversal_requested', 'administrative_act', 'PESSOA_48291307' FROM r RETURNING item_id";

    private const string Authorization =
        "WITH a AS (INSERT INTO content.reversal_authorization (domain_id, request_id, authorized_by, valid_until) "
        + "VALUES (@domain, @request, @person, now() + @term::interval) RETURNING id, request_id) "
        + "INSERT INTO public.custody_event (actor_person_id, actor_system, item_kind, item_id, step, reason, reference) "
        + "SELECT @person, false, 'reversal_authorization', a.id, 'reversal_authorized', 'administrative_act', a.request_id::text FROM a RETURNING item_id";

    /// <summary>The authorization of a request, by the person, that expired a day ago: the bound of the term is set aside for the one statement, as only the superuser can.</summary>
    public static Task ExpiredAsync(ContentWorld world, (int Domain, long Request) of, long personId) =>
        world.Database.ExecuteAsSuperuserAsync(
            "ALTER TABLE content.reversal_authorization DISABLE TRIGGER reversal_authorization_is_bounded; "
            + Authorization.Replace("now() + @term::interval", "now() - interval '1 day'", StringComparison.Ordinal)
            + "; ALTER TABLE content.reversal_authorization ENABLE ALWAYS TRIGGER reversal_authorization_is_bounded",
            ("domain", of.Domain), ("request", of.Request), ("person", personId));

    /// <summary>A request of the person in the domain, with its event; the identifier of the request.</summary>
    public static async Task<long> RequestAsync(ContentWorld world, int domainId, long personId) =>
        (long)(await world.InDomainAsync(domainId, Request, ("domain", domainId), ("person", personId)))!;

    /// <summary>An authorization of the request by the person, for the term given as an interval such as "30 days", with its event; the identifier of the authorization.</summary>
    public static async Task<long> AuthorizeAsync(ContentWorld world, (int Domain, long Request) of, long personId, string term) =>
        (long)(await world.InDomainAsync(of.Domain, Authorization, ("domain", of.Domain), ("request", of.Request), ("person", personId), ("term", term)))!;
}
