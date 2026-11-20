namespace Core.Persistence.Content;

/// <summary>The statements of the rerun of the sanitization (0064): the queue of the domain declared and the change of the sanitization of an item, never a word of it.</summary>
public static partial class ContentQueries
{
    /// <summary>The newest open request, with the version it reruns to: one with an item not concluded.</summary>
    internal const string ReadRerunOpen =
        "SELECT r.id, r.target_version FROM content.sanitization_rerun r "
        + "WHERE EXISTS (SELECT 1 FROM content.sanitization_rerun_item q WHERE q.rerun_id = r.id AND q.state <> 'done') ORDER BY r.id DESC LIMIT 1";

    /// <summary>One item a request to the version would queue: of an accepted batch, not emptied, of another version, and in no open request.</summary>
    internal const string ReadRerunPending =
        "SELECT i.id FROM content.item_text i JOIN content.ingestion_batch b ON b.id = i.batch_id AND b.domain_id = i.domain_id "
        + "WHERE b.state = 'accepted' AND i.emptied_at IS NULL AND i.sanitization_version IS DISTINCT FROM @version "
        + "AND NOT EXISTS (SELECT 1 FROM content.sanitization_rerun_item q WHERE q.item_id = i.id AND q.state <> 'done') LIMIT 1";

    /// <summary>Writes the request, the version once per domain, and queues every item <see cref="ReadRerunPending"/> would find; answers the request.</summary>
    internal const string InsertSanitizationRerun =
        "WITH version AS (INSERT INTO content.sanitization_version (domain_id, version, steps) SELECT @domain::integer, @version::text, @steps::text WHERE @version::text IS NOT NULL "
        + "ON CONFLICT (version, domain_id) DO NOTHING), "
        + "rerun AS (INSERT INTO content.sanitization_rerun (domain_id, target_version, requested_by, requested_at) VALUES (@domain, @version, @person, @now) RETURNING id), "
        + "queued AS (INSERT INTO content.sanitization_rerun_item (domain_id, rerun_id, item_id) SELECT @domain, rerun.id, i.id FROM rerun, content.item_text i "
        + "JOIN content.ingestion_batch b ON b.id = i.batch_id AND b.domain_id = i.domain_id WHERE b.state = 'accepted' AND i.emptied_at IS NULL "
        + "AND i.sanitization_version IS DISTINCT FROM @version AND NOT EXISTS (SELECT 1 FROM content.sanitization_rerun_item q WHERE q.item_id = i.id AND q.state <> 'done') "
        + "RETURNING rerun_id) SELECT id FROM rerun";

    /// <summary>Concludes as superseded the items not concluded of the requests to another version, answering each item and the version its request reran to.</summary>
    internal const string CloseRerunSuperseded =
        "UPDATE content.sanitization_rerun_item q SET state = 'done', result = 'superseded', concluded_at = @now::timestamptz, hold_until = NULL "
        + "FROM content.sanitization_rerun r WHERE r.id = q.rerun_id AND r.target_version <> @version AND q.state <> 'done' RETURNING q.item_id, r.target_version";

    /// <summary>Concludes as refused the items of the version whose hold expired at the last try, which spends it; answers each item.</summary>
    internal const string CloseRerunExhausted =
        "UPDATE content.sanitization_rerun_item q SET state = 'done', result = 'refused', refusal = 'failed', concluded_at = @now::timestamptz, hold_until = NULL, tries = q.tries + 1 "
        + "FROM content.sanitization_rerun r WHERE r.id = q.rerun_id AND r.target_version = @version AND q.state = 'held' AND q.hold_until <= @now::timestamptz "
        + "AND q.tries + 1 >= @tries::integer RETURNING q.item_id";

    /// <summary>Holds one item of the version until the moment given, queued or whose hold expired, which spends a try; one no other session holds the lock of.</summary>
    internal const string CloseRerunHold =
        "UPDATE content.sanitization_rerun_item q SET state = 'held', hold_until = @until::timestamptz, tries = q.tries + (q.state = 'held')::integer "
        + "FROM (SELECT c.id FROM content.sanitization_rerun_item c JOIN content.sanitization_rerun r ON r.id = c.rerun_id WHERE r.target_version = @version "
        + "AND (c.state = 'queued' OR (c.state = 'held' AND c.hold_until <= @now::timestamptz)) ORDER BY c.id LIMIT 1 FOR UPDATE OF c SKIP LOCKED) t "
        + "WHERE q.id = t.id RETURNING q.id, q.rerun_id, q.item_id, q.tries, q.hold_until";

    /// <summary>Gives back to the queue an item still under the hold given, spending no try.</summary>
    internal const string CloseRerunReturned =
        "UPDATE content.sanitization_rerun_item SET state = 'queued', hold_until = NULL WHERE id = @entry AND state = 'held' AND hold_until = @held::timestamptz";

    /// <summary>Spends a try of an item still under the hold given: back to the queue, or refused at the last try; answers the state it is left in.</summary>
    internal const string CloseRerunFailed =
        "UPDATE content.sanitization_rerun_item SET tries = tries + 1, hold_until = NULL, state = CASE WHEN tries + 1 >= @tries::integer THEN 'done' ELSE 'queued' END, "
        + "result = CASE WHEN tries + 1 >= @tries::integer THEN 'refused' END, refusal = CASE WHEN tries + 1 >= @tries::integer THEN 'failed' END, "
        + "concluded_at = CASE WHEN tries + 1 >= @tries::integer THEN @now::timestamptz END WHERE id = @entry AND state = 'held' AND hold_until = @held::timestamptz RETURNING state";

    /// <summary>Concludes an item still under the hold given, with its result and the reason of a refusal.</summary>
    internal const string CloseRerunItem =
        "UPDATE content.sanitization_rerun_item SET state = 'done', result = @result::text, refusal = @refusal::text, concluded_at = @now::timestamptz, hold_until = NULL "
        + "WHERE id = @entry AND state = 'held' AND hold_until = @held::timestamptz RETURNING id";

    /// <summary>What the rerun reads of an item: the sealed raw text, occurrences and sanitized text, the version, whether it was emptied, and the key wrapped with its epoch.</summary>
    internal const string ReadResanitizationSource =
        "SELECT raw_text, occurrences, sanitized_text, sanitization_version, emptied_at IS NOT NULL, dek_epoch, dek_wrapped FROM content.item_text WHERE id = @id";

    /// <summary>Writes the new sanitization of an item not emptied, its version once per domain; the database lets it through only with the rerun in the same transaction.</summary>
    internal const string CloseItemResanitization =
        "WITH version AS (INSERT INTO content.sanitization_version (domain_id, version, steps) SELECT @domain::integer, @version::text, @steps::text WHERE @version::text IS NOT NULL "
        + "ON CONFLICT (version, domain_id) DO NOTHING) UPDATE content.item_text SET sanitized_text = @sanitized, sanitization_version = @version, sanitization_trail = @trail, "
        + "sanitization_counts = @counts, sanitization_untreated = @untreated WHERE id = @id AND emptied_at IS NULL";

    /// <summary>As <see cref="CloseItemResanitization"/>, keeping the sanitized text, which the rerun produced again as it was.</summary>
    internal const string CloseItemRecount =
        "WITH version AS (INSERT INTO content.sanitization_version (domain_id, version, steps) SELECT @domain::integer, @version::text, @steps::text WHERE @version::text IS NOT NULL "
        + "ON CONFLICT (version, domain_id) DO NOTHING) UPDATE content.item_text SET sanitization_version = @version, sanitization_trail = @trail, "
        + "sanitization_counts = @counts, sanitization_untreated = @untreated WHERE id = @id AND emptied_at IS NULL";

    /// <summary>How many items of the accepted batches, not emptied, each version holds, the most recent first and the items with none last.</summary>
    internal const string ReadRerunSituation =
        "SELECT i.sanitization_version, count(*) FROM content.item_text i JOIN content.ingestion_batch b ON b.id = i.batch_id AND b.domain_id = i.domain_id "
        + "LEFT JOIN content.sanitization_version v ON v.version = i.sanitization_version AND v.domain_id = i.domain_id "
        + "WHERE b.state = 'accepted' AND i.emptied_at IS NULL GROUP BY i.sanitization_version, v.id ORDER BY v.id DESC NULLS LAST";

    /// <summary>A request: its version, when it was asked, its items by state and by result, and when the last one was concluded.</summary>
    internal const string ReadRerunProgress =
        "SELECT r.target_version, r.requested_at, count(*) FILTER (WHERE q.state = 'queued'), count(*) FILTER (WHERE q.state = 'held'), "
        + "count(*) FILTER (WHERE q.result = 'identical'), count(*) FILTER (WHERE q.result = 'replaced'), count(*) FILTER (WHERE q.result = 'refused'), "
        + "count(*) FILTER (WHERE q.result = 'superseded'), max(q.concluded_at) "
        + "FROM content.sanitization_rerun r JOIN content.sanitization_rerun_item q ON q.rerun_id = r.id WHERE r.id = @rerun GROUP BY r.id";

    /// <summary>The items a request refused, each with its batch and the reason, in the order they were queued.</summary>
    internal const string ReadRerunRefusals =
        "SELECT q.item_id, i.batch_id, q.refusal FROM content.sanitization_rerun_item q JOIN content.item_text i ON i.id = q.item_id "
        + "WHERE q.rerun_id = @rerun AND q.result = 'refused' ORDER BY q.id";
}
