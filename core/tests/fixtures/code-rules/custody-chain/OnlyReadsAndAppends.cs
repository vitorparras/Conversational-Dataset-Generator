namespace Fixture.CustodyChain;

internal static class OnlyReadsAndAppends
{
    public const string Append = "INSERT INTO public.custody_event (step) VALUES (@step) RETURNING sequence";

    public const string Read = "SELECT sequence, digest FROM public.custody_event ORDER BY sequence";

    public const string Repair = "ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed";

    public const string OtherTable = "UPDATE public.person SET active = false WHERE id = @id";

    public const string Sentence = "the application may update the chain only by adding an event; custody_event is never deleted";

    public const string ReadJoined = "SELECT sequence FROM " + "public.custody_event" + " ORDER BY sequence";

    public const string ReadByName = $"{Select} FROM {Table} ORDER BY sequence";

    private const string Select = "SELECT digest";
    private const string Table = "public.custody_event";
}
