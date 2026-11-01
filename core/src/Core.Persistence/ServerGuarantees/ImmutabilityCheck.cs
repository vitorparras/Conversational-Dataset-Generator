using System.Data;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Refuses to start when the two triggers that keep a custody event from being changed, removed or emptied away are not on <c>public.custody_event</c>, as the migration declared them
/// and enabled for every session, or when the function either one calls is not the one the migration wrote: a disabled or replaced trigger, or a function replaced by one that lets the change
/// through, leaves the chain open to the owner's credential even when its links are whole. The triggers and the functions are read from the system catalog with the application's credential;
/// no event is read.
/// </summary>
public sealed class ImmutabilityCheck : IStartupCheck
{
    internal const string ChangedTrigger = "custody_event_never_changed";
    internal const string EmptiedTrigger = "custody_event_never_emptied";
    internal const string ChangedFunction = "refuse_custody_event_change";
    internal const string EmptiedFunction = "refuse_custody_event_emptying";

    /// <summary>
    /// The text of the body of each function, as the migration 0009 writes it between its dollar quotes and as the catalog keeps it in prosrc. The one place this text lives besides the migration:
    /// a test derives it from the embedded migration and fails when the two differ.
    /// </summary>
    internal const string ChangedBody =
        "\nBEGIN\n    RAISE EXCEPTION 'public.custody_event: % refused for the event of sequence %', TG_OP, OLD.sequence\n"
        + "        USING ERRCODE = 'CE001', SCHEMA = 'public', TABLE = 'custody_event';\nEND\n";

    internal const string EmptiedBody =
        "\nBEGIN\n    RAISE EXCEPTION 'public.custody_event: % refused', TG_OP\n"
        + "        USING ERRCODE = 'CE001', SCHEMA = 'public', TABLE = 'custody_event';\nEND\n";

    internal const string ExpectedLanguage = "plpgsql";

    // tgtype: ROW 1, BEFORE 2, DELETE 8, UPDATE 16, TRUNCATE 32. A trigger of the name that fires on another event, at another moment or through another function is not the one declared,
    // and reads as absent. The mode is tgenabled: 'A' fires always, 'O' only in an ordinary session, 'R' only in a replica session, 'D' never. A table that is not there answers null for all.
    private const string Query = $"""
        SELECT pg_catalog.max(t.tgenabled::text) FILTER (WHERE {ChangedMatch}) AS changed,
            pg_catalog.max(p.prosrc) FILTER (WHERE {ChangedMatch}) AS changed_body,
            pg_catalog.max(l.lanname::text) FILTER (WHERE {ChangedMatch}) AS changed_language,
            pg_catalog.max(t.tgenabled::text) FILTER (WHERE {EmptiedMatch}) AS emptied,
            pg_catalog.max(p.prosrc) FILTER (WHERE {EmptiedMatch}) AS emptied_body,
            pg_catalog.max(l.lanname::text) FILTER (WHERE {EmptiedMatch}) AS emptied_language
        FROM pg_catalog.pg_trigger t
        JOIN pg_catalog.pg_proc p ON p.oid = t.tgfoid
        JOIN pg_catalog.pg_language l ON l.oid = p.prolang
        JOIN pg_catalog.pg_namespace n ON n.oid = p.pronamespace AND n.nspname = 'public'
        WHERE t.tgrelid = pg_catalog.to_regclass('public.custody_event') AND NOT t.tgisinternal
        """;

    // The trigger as the migration wrote it: no WHEN condition, no column list after UPDATE OF and no arguments. One recreated with any of them fires on less than the migration made it fire on.
    private const string Plain = "t.tgqual IS NULL AND t.tgattr::text = '' AND t.tgnargs = 0";

    private const string ChangedMatch = $"t.tgname = '{ChangedTrigger}' AND t.tgtype = 27 AND {Plain} AND p.proname = '{ChangedFunction}'";
    private const string EmptiedMatch = $"t.tgname = '{EmptiedTrigger}' AND t.tgtype = 34 AND {Plain} AND p.proname = '{EmptiedFunction}'";

    private readonly NpgsqlDataSource dataSource;

    /// <summary>The check reads through the data source built with the application's credential.</summary>
    public ImmutabilityCheck(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <inheritdoc />
    public string Name => "Immutability";

    /// <inheritdoc />
    public Task<CheckResult> RunAsync(CancellationToken cancellationToken) =>
        ServerProbe.RunAsync(
            dataSource,
            new Guarantee(Name, "o papel da aplicação precisa ler pg_trigger, pg_proc, pg_language e pg_namespace, presentes em todo PostgreSQL", ReadAsync),
            cancellationToken);

    /// <summary>Judges each trigger as the catalog holds it: its mode, and the body and the language of the function it calls. A reading with no mode is a trigger that is not on the table.</summary>
    internal static Finding Judge(TriggerReading changed, TriggerReading emptied)
    {
        var problems = Problems(ChangedTrigger, ChangedFunction, ChangedBody, changed).Concat(Problems(EmptiedTrigger, EmptiedFunction, EmptiedBody, emptied)).ToArray();
        return problems.Length == 0
            ? Finding.Holds("os gatilhos que recusam alterar, remover e esvaziar public.custody_event estão presentes, habilitados para toda sessão e chamam as funções da migração")
            : Finding.Missing(string.Join("; ", problems));
    }

    private static IEnumerable<string> Problems(string trigger, string function, string expectedBody, TriggerReading reading)
    {
        if (reading.Mode is null)
        {
            yield return $"o gatilho {trigger} não está em public.custody_event como a migração 0009 o declara: recrie-o com a definição dela e execute "
                + $"ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER {trigger}";
            yield break;
        }

        if (reading.Mode != "A")
        {
            var how = reading.Mode == "D" ? "está desabilitado" : "não dispara em toda sessão";
            yield return $"o gatilho {trigger} de public.custody_event {how}: execute ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER {trigger}";
        }

        if (!string.Equals(reading.Body, expectedBody, StringComparison.Ordinal) || !string.Equals(reading.Language, ExpectedLanguage, StringComparison.Ordinal))
        {
            yield return $"a função public.{function}, que o gatilho {trigger} chama, não tem o corpo nem a linguagem que a migração 0009 escreveu: "
                + "restaure-a com a definição dela (CREATE OR REPLACE FUNCTION), porque uma função diferente pode deixar a mudança passar";
        }
    }

    private static Task<Finding> ReadAsync(NpgsqlConnection connection, CancellationToken cancellationToken) =>
        ServerProbe.ReadRowAsync(connection, Query, Read, cancellationToken);

    private static Finding Read(IDataRecord row) => Judge(
        new TriggerReading(row["changed"] as string, row["changed_body"] as string, row["changed_language"] as string),
        new TriggerReading(row["emptied"] as string, row["emptied_body"] as string, row["emptied_language"] as string));

    /// <summary>What the catalog holds about one trigger: its mode, and the body and the language of the function it calls; all null where the trigger declared is not on the table.</summary>
    internal sealed record TriggerReading(string? Mode, string? Body, string? Language)
    {
        /// <summary>A trigger in the given mode that calls the function as the migration wrote it.</summary>
        internal static TriggerReading Declared(string mode, string body) => new(mode, body, ExpectedLanguage);

        internal static TriggerReading Absent { get; } = new(null, null, null);
    }
}
