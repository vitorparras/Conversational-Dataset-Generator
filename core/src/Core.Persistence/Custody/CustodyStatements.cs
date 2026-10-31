namespace Core.Persistence.Custody;

/// <summary>The one statement that adds an event: only what the caller knows is named; the sequence, the moment and both digests are the server's.</summary>
internal static class CustodyStatements
{
    internal const string ActorConstraint = "custody_event_actor_fkey";
    internal const string ActorActiveConstraint = "custody_event_actor_active";
    internal const string CorrectsConstraint = "custody_event_corrects_fkey";

    internal const string Append = """
        INSERT INTO public.custody_event (actor_person_id, actor_system, item_kind, item_id, step, reason, corrects)
        VALUES (@actor, @system, @kind, @item, @step, @reason, @corrects)
        RETURNING sequence
        """;
}
