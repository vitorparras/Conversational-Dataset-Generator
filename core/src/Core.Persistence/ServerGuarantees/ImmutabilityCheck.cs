using System.Data;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Refuses to start when the two triggers that keep a custody event from being changed, removed or emptied away are not on <c>public.custody_event</c>, as the migration declared them
/// and enabled for every session: a disabled or replaced trigger leaves the chain open to the owner's credential even when its links are whole.
/// The triggers are read from the system catalog with the application's credential; no event is read.
/// </summary>
public sealed class ImmutabilityCheck : IStartupCheck
{
    internal const string ChangedTrigger = "custody_event_never_changed";
    internal const string EmptiedTrigger = "custody_event_never_emptied";

    private const string ChangedFunction = "refuse_custody_event_change";
    private const string EmptiedFunction = "refuse_custody_event_emptying";

    // tgtype: ROW 1, BEFORE 2, DELETE 8, UPDATE 16, TRUNCATE 32. A trigger of the name that fires on another event, at another moment or through another function is not the one declared,
    // and reads as absent. The mode is tgenabled: 'A' fires always, 'O' only in an ordinary session, 'R' only in a replica session, 'D' never. A table that is not there answers null for both.
    private const string Query = $"""
        SELECT pg_catalog.max(t.tgenabled::text) FILTER (WHERE t.tgname = '{ChangedTrigger}' AND t.tgtype = 27 AND p.proname = '{ChangedFunction}') AS changed,
            pg_catalog.max(t.tgenabled::text) FILTER (WHERE t.tgname = '{EmptiedTrigger}' AND t.tgtype = 34 AND p.proname = '{EmptiedFunction}') AS emptied
        FROM pg_catalog.pg_trigger t
        JOIN pg_catalog.pg_proc p ON p.oid = t.tgfoid
        JOIN pg_catalog.pg_namespace n ON n.oid = p.pronamespace AND n.nspname = 'public'
        WHERE t.tgrelid = pg_catalog.to_regclass('public.custody_event') AND NOT t.tgisinternal
        """;

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
            new Guarantee(Name, "o papel da aplicação precisa ler pg_trigger, pg_proc e pg_namespace, presentes em todo PostgreSQL", ReadAsync),
            cancellationToken);

    /// <summary>Judges the mode of each trigger as the catalog holds it; null where the trigger declared is not on the table.</summary>
    internal static Finding Judge(string? changed, string? emptied)
    {
        var problems = new[] { Problem(ChangedTrigger, changed), Problem(EmptiedTrigger, emptied) }.Where(problem => problem is not null).ToArray();
        return problems.Length == 0
            ? Finding.Holds("os gatilhos que recusam alterar, remover e esvaziar public.custody_event estão presentes e habilitados para toda sessão")
            : Finding.Missing(string.Join("; ", problems));
    }

    private static string? Problem(string trigger, string? mode) => mode switch
    {
        "A" => null,
        null => $"o gatilho {trigger} não está em public.custody_event como a migração 0009 o declara: recrie-o com a definição dela e execute "
            + $"ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER {trigger}",
        "D" => $"o gatilho {trigger} de public.custody_event está desabilitado: execute ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER {trigger}",
        _ => $"o gatilho {trigger} de public.custody_event não dispara em toda sessão: execute ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER {trigger}",
    };

    private static Task<Finding> ReadAsync(NpgsqlConnection connection, CancellationToken cancellationToken) =>
        ServerProbe.ReadRowAsync(connection, Query, Read, cancellationToken);

    private static Finding Read(IDataRecord row) => Judge(row["changed"] as string, row["emptied"] as string);
}
