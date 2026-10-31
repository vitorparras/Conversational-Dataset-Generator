namespace Core.Persistence.People;

/// <summary>Setting a password writes the derivation and the record of the act in one command; the record holds the act and never a value of the password.</summary>
internal static class CredentialStatements
{
    internal const string Set = $"""
        WITH target AS (SELECT id FROM public.person WHERE id = @id AND active AND {PersonStatements.ActorActive}),
        stored AS (
            INSERT INTO public.person_credential (person_id, algorithm, work_factor, salt, derived)
            SELECT id, @algorithm, @factor, @salt, @derived FROM target
            ON CONFLICT (person_id) DO UPDATE SET algorithm = EXCLUDED.algorithm, work_factor = EXCLUDED.work_factor,
                salt = EXCLUDED.salt, derived = EXCLUDED.derived, derived_at = now()
            RETURNING person_id)
        INSERT INTO public.person_change (person_id, kind, actor_id)
        SELECT person_id, 'password_set', @actor FROM stored
        RETURNING person_id
        """;

    internal const string Read = "SELECT algorithm, work_factor, salt, derived FROM public.person_credential WHERE person_id = @id";

    /// <summary>Replaces the derivation only while it is the one that was read and a lower factor, so that a password set in the meantime is never overwritten by the old one.</summary>
    internal const string Recompute = """
        UPDATE public.person_credential
        SET algorithm = @algorithm, work_factor = @factor, salt = @salt, derived = @derived, derived_at = now()
        WHERE person_id = @id AND derived = @previous AND work_factor < @factor
        RETURNING person_id
        """;
}
