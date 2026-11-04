namespace Core.Persistence.People;

internal static class SignInStatements
{
    internal const string Insert = "INSERT INTO public.sign_in_attempt (network) VALUES (@network::cidr) RETURNING id";

    internal const string Series = """
        SELECT a.network::text, count(*)::int, min(a.occurred_at), max(a.occurred_at)
        FROM public.sign_in_attempt a
        JOIN public.custody_event e ON e.item_kind = 'sign_in_attempt' AND e.item_id = a.id AND e.step = 'sign_in_refused'
        WHERE a.network IS NOT NULL AND a.occurred_at > @until - @window AND a.occurred_at <= @until
        GROUP BY a.network
        HAVING count(*) >= @least
        ORDER BY count(*) DESC, a.network
        """;
}
