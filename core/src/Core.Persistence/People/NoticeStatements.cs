namespace Core.Persistence.People;

/// <summary>The statements over the notices of a person; the codes are the ones the table lists, and nothing free is bound.</summary>
internal static class NoticeStatements
{
    internal const string PasswordReset = "password_reset";
    internal const string DomainReadOnly = "domain_read_only";
    internal const string Insert = "INSERT INTO public.person_notice (person_id, code, sessions_ended) VALUES (@person, @code, @sessions)";
    internal const string Unread = "SELECT id, code, sessions_ended, created_at FROM public.person_notice WHERE person_id = @person AND read_at IS NULL ORDER BY id DESC";
    internal const string MarkRead =
        "WITH marked AS (UPDATE public.person_notice SET read_at = clock_timestamp() WHERE id = @id AND person_id = @person AND read_at IS NULL RETURNING id) " +
        "SELECT EXISTS (SELECT 1 FROM public.person_notice WHERE id = @id AND person_id = @person)";
}
