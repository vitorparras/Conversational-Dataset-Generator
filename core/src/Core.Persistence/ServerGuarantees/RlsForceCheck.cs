using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Checks that every table with a row policy applies it with FORCE, so that the policy holds even against the table's owner;
/// a policy on a table whose row security is off counts as a policy that does not apply. The content schema is held to more, since
/// a content table born wrong works and returns what is asked, and only this check can tell: every table there carries the domain
/// column tied by a foreign key to the domain registry, a policy, and no permissive policy beyond the domain one (permissive policies add up, so one more that lets everything
/// through opens the table); and nothing there may be a view, which has no row policy of its own.
/// </summary>
public sealed class RlsForceCheck : IStartupCheck
{
    // A permissive policy is the domain one when its expressions, stripped of blanks, parentheses and the schema, read domain_id=declared_domain.
    private const string Query = """
        WITH content AS (
            SELECT t.oid, t.relkind FROM pg_catalog.pg_class t JOIN pg_catalog.pg_namespace n ON n.oid = t.relnamespace WHERE n.nspname = 'content')
        SELECT pg_catalog.count(*) FILTER (WHERE c.relrowsecurity AND c.relforcerowsecurity) AS forced,
            pg_catalog.min(c.oid::regclass::text) FILTER (WHERE NOT (c.relrowsecurity AND c.relforcerowsecurity)) AS unforced,
            (SELECT pg_catalog.min(t.oid::regclass::text) FROM content t WHERE t.relkind IN ('r', 'p')
                AND NOT EXISTS (SELECT FROM pg_catalog.pg_attribute a
                    WHERE a.attrelid = t.oid AND a.attname = 'domain_id' AND a.attnotnull AND NOT a.attisdropped)) AS without_domain,
            (SELECT pg_catalog.min(t.oid::regclass::text) FROM content t WHERE t.relkind IN ('r', 'p')
                AND NOT EXISTS (SELECT FROM pg_catalog.pg_constraint k JOIN pg_catalog.pg_attribute a ON a.attrelid = k.conrelid AND a.attname = 'domain_id'
                    WHERE k.conrelid = t.oid AND k.contype = 'f' AND k.confrelid = pg_catalog.to_regclass('public.domain')
                        AND k.conkey = ARRAY[a.attnum])) AS without_domain_key,
            (SELECT pg_catalog.min(t.oid::regclass::text) FROM content t WHERE t.relkind IN ('r', 'p')
                AND NOT EXISTS (SELECT FROM pg_catalog.pg_policy p WHERE p.polrelid = t.oid)) AS without_policy,
            (SELECT pg_catalog.min(t.oid::regclass::text) FROM content t WHERE t.relkind IN ('r', 'p')
                AND EXISTS (SELECT FROM pg_catalog.pg_policy p WHERE p.polrelid = t.oid AND p.polpermissive AND (
                    pg_catalog.regexp_replace(coalesce(pg_catalog.pg_get_expr(p.polqual, p.polrelid), 'domain_id=declared_domain'),
                        '[\s()]|public\.', '', 'g') <> 'domain_id=declared_domain'
                    OR pg_catalog.regexp_replace(coalesce(pg_catalog.pg_get_expr(p.polwithcheck, p.polrelid), 'domain_id=declared_domain'),
                        '[\s()]|public\.', '', 'g') <> 'domain_id=declared_domain'))) AS open_policy,
            (SELECT pg_catalog.min(t.oid::regclass::text) FROM content t WHERE t.relkind IN ('v', 'm', 'f')) AS not_a_table
        FROM pg_catalog.pg_class c
        WHERE c.relkind IN ('r', 'p')
            AND (c.relrowsecurity OR EXISTS (SELECT FROM pg_catalog.pg_policy p WHERE p.polrelid = c.oid))
        """;

    private readonly NpgsqlDataSource dataSource;

    /// <summary>The check reads through the data source built with the application's credential.</summary>
    public RlsForceCheck(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <inheritdoc />
    public string Name => "RlsForce";

    /// <inheritdoc />
    public Task<CheckResult> RunAsync(CancellationToken cancellationToken) =>
        ServerProbe.RunAsync(
            dataSource,
            new Guarantee(Name, "use um servidor PostgreSQL que ofereça FORCE ROW LEVEL SECURITY", ReadAsync),
            cancellationToken);

    internal static Finding Judge(long forcedTables, string? unforcedTable, ContentFindings? content = null)
    {
        content ??= new ContentFindings();
        return (content, unforcedTable) switch
        {
            ({ NotATable: not null }, _) => Finding.Missing(
                $"o objeto {content.NotATable} do esquema content não é tabela: visão não tem política de linha e devolve linhas sem domínio; "
                + "o esquema content só guarda tabelas criadas com public.create_content_table"),
            ({ WithoutDomain: not null }, _) => Finding.Missing(
                $"a tabela de conteúdo {content.WithoutDomain} não tem a coluna domain_id não nula: crie as tabelas do esquema content com public.create_content_table"),
            ({ WithoutDomainKey: not null }, _) => Finding.Missing(
                $"a tabela de conteúdo {content.WithoutDomainKey} tem domain_id sem chave estrangeira para public.domain: um domínio que não existe seria aceito; "
                + "crie as tabelas do esquema content com public.create_content_table"),
            ({ WithoutPolicy: not null }, _) => Finding.Missing(
                $"a tabela de conteúdo {content.WithoutPolicy} não tem política de linha por domínio: crie as tabelas do esquema content com public.create_content_table"),
            ({ OpenPolicy: not null }, _) => Finding.Missing(
                $"a tabela de conteúdo {content.OpenPolicy} tem política permissiva que não se limita a domain_id = declared_domain(): política permissiva soma às outras "
                + "e abre a tabela; remova-a com DROP POLICY"),
            (_, not null) => Finding.Missing(
                $"a tabela {unforcedTable} tem política de linha que não vale contra o dono: execute "
                + $"ALTER TABLE {unforcedTable} ENABLE ROW LEVEL SECURITY e ALTER TABLE {unforcedTable} FORCE ROW LEVEL SECURITY"),
            _ => Finding.Holds($"{forcedTables} tabela(s) com política de linha, todas com FORCE ROW LEVEL SECURITY"),
        };
    }

    private static Task<Finding> ReadAsync(NpgsqlConnection connection, CancellationToken cancellationToken) =>
        ServerProbe.ReadRowAsync(
            connection,
            Query,
            row => Judge(
                (long)row["forced"],
                row["unforced"] as string,
                new ContentFindings(
                    row["without_domain"] as string,
                    row["without_policy"] as string,
                    row["open_policy"] as string,
                    row["not_a_table"] as string,
                    row["without_domain_key"] as string)),
            cancellationToken);

    /// <summary>The first content table or object, by name, that fails each rule of the content schema; null where none does.</summary>
    internal sealed record ContentFindings(
        string? WithoutDomain = null, string? WithoutPolicy = null, string? OpenPolicy = null, string? NotATable = null, string? WithoutDomainKey = null);
}
