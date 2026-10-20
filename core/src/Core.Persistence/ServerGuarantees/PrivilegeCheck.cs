using System.Data;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Checks that the application's role is no superuser, does not bypass row security, and answers as owner of neither the
/// database, nor a schema, nor a table, whether by owning it or by belonging to the role that does.
/// </summary>
public sealed class PrivilegeCheck : IStartupCheck
{
    private const string Query = """
        SELECT r.rolname::text AS role, r.rolsuper AS superuser, r.rolbypassrls AS bypass,
            (SELECT d.datname::text FROM pg_database d
                WHERE d.datname = current_database() AND pg_has_role(d.datdba, 'MEMBER')) AS owned_database,
            (SELECT min(n.nspname::text) FROM pg_namespace n WHERE pg_has_role(n.nspowner, 'MEMBER')) AS owned_schema,
            (SELECT min(c.oid::regclass::text) FROM pg_class c
                WHERE c.relkind IN ('r', 'p') AND pg_has_role(c.relowner, 'MEMBER')) AS owned_table
        FROM pg_roles r
        WHERE r.rolname = current_user
        """;

    private readonly NpgsqlDataSource dataSource;

    /// <summary>The check reads through the data source built with the application's credential.</summary>
    public PrivilegeCheck(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <inheritdoc />
    public string Name => "Privilege";

    /// <inheritdoc />
    public Task<CheckResult> RunAsync(CancellationToken cancellationToken) =>
        ServerProbe.RunAsync(
            dataSource,
            new Guarantee(Name, "o papel da aplicação precisa ler pg_roles e pg_has_role, presentes em todo PostgreSQL", ReadAsync),
            cancellationToken);

    internal static Finding Judge(RoleReading role) => role switch
    {
        { Superuser: true } => Finding.Missing(
            $"o papel '{role.Role}' é superusuário e desabilita gatilho e política de linha: execute ALTER ROLE {role.Role} NOSUPERUSER"),
        { BypassRls: true } => Finding.Missing(
            $"o papel '{role.Role}' tem BYPASSRLS e escapa da política de linha: execute ALTER ROLE {role.Role} NOBYPASSRLS"),
        { Database: { } database } => Owner(role.Role, "do banco", "DATABASE", database),
        { Schema: { } schema } => Owner(role.Role, "do esquema", "SCHEMA", schema),
        { Table: { } table } => Owner(role.Role, "da tabela", "TABLE", table),
        _ => Finding.Holds($"o papel '{role.Role}' não é superusuário, não tem BYPASSRLS e não é dono do banco, de esquema nem de tabela"),
    };

    private static Finding Owner(string role, string what, string kind, string name) =>
        Finding.Missing(
            $"o papel '{role}' responde como dono {what} {name}, por posse ou por pertença ao papel dono: transfira a posse com "
            + $"ALTER {kind} {name} OWNER TO <papel dono>, ou revogue a pertença com REVOKE <papel dono> FROM {role}");

    private static Task<Finding> ReadAsync(NpgsqlConnection connection, CancellationToken cancellationToken) =>
        ServerProbe.ReadRowAsync(connection, server => new NpgsqlCommand(Query, server), Read, cancellationToken);

    private static Finding Read(IDataRecord row) =>
        Judge(new RoleReading(
            (string)row["role"],
            (bool)row["superuser"],
            (bool)row["bypass"],
            row["owned_database"] as string,
            row["owned_schema"] as string,
            row["owned_table"] as string));

    /// <summary>What the server says about the role the application connected as.</summary>
    internal sealed record RoleReading(string Role, bool Superuser, bool BypassRls, string? Database, string? Schema, string? Table);
}
