namespace Core.Persistence.Custody;

/// <summary>The one statement that adds an event: only what the caller knows is named; the sequence, the moment and both digests are the server's.</summary>
internal static class CustodyStatements
{
    internal const string ActorConstraint = "custody_event_actor_fkey";
    internal const string ActorActiveConstraint = "custody_event_actor_active";
    internal const string CorrectsConstraint = "custody_event_corrects_fkey";

    /// <summary>The SQLSTATE the triggers of the table raise when a change, a removal or an emptying is attempted (migration 0009).</summary>
    internal const string ImmutableSqlState = "CE001";

    /// <summary>The SQLSTATE raised by the append, and by a write of content, while the last verification of the chain failed (migration 0012).</summary>
    internal const string ChainBrokenSqlState = "CE002";

    /// <summary>Every field of every event that the digest covers, with the two digests, in the order of the sequence: what the verification reads.</summary>
    internal const string ReadChain = """
        SELECT sequence, occurred_at, actor_person_id, actor_system, item_kind, item_id, step, reason, corrects, previous_digest, digest, recorded_by
        FROM public.custody_event
        ORDER BY sequence
        """;

    internal const string Append = """
        INSERT INTO public.custody_event (actor_person_id, actor_system, item_kind, item_id, step, reason, corrects)
        VALUES (@actor, @system, @kind, @item, @step, @reason, @corrects)
        RETURNING sequence
        """;
}
