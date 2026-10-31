namespace Core.Persistence.People;

/// <summary>
/// Each statement writes the person and the record of the change in one command, so that both exist or neither does.
/// Whoever acts must be nobody as a person or a person who is active.
/// </summary>
internal static class PersonStatements
{
    internal const string ActorActive = "(@actor IS NULL OR EXISTS (SELECT 1 FROM public.person actor WHERE actor.id = @actor AND actor.active))";

    internal const string Create = $"""
        WITH created AS (INSERT INTO public.person (name, function_name) SELECT @name, @function WHERE {ActorActive} RETURNING id)
        INSERT INTO public.person_change (person_id, kind, new_function, actor_id)
        SELECT id, 'created', @function, @actor FROM created
        RETURNING person_id
        """;

    internal const string Deactivate = $"""
        WITH changed AS (UPDATE public.person SET active = false WHERE id = @id AND active AND {ActorActive} RETURNING id)
        INSERT INTO public.person_change (person_id, kind, actor_id)
        SELECT id, 'deactivated', @actor FROM changed
        RETURNING person_id
        """;

    internal const string ChangeFunction = $"""
        WITH prior AS (SELECT id, function_name FROM public.person WHERE id = @id AND active AND function_name <> @function AND {ActorActive} FOR UPDATE),
        changed AS (UPDATE public.person p SET function_name = @function FROM prior WHERE p.id = prior.id RETURNING p.id)
        INSERT INTO public.person_change (person_id, kind, previous_function, new_function, actor_id)
        SELECT prior.id, 'function_changed', prior.function_name, @function, @actor FROM prior JOIN changed ON changed.id = prior.id
        RETURNING person_id
        """;

    internal const string IsActive = "SELECT active FROM public.person WHERE id = @id";
}
