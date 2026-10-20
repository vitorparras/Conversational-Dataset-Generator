using System.Data;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Checks that the application's role neither is nor can become, through SET ROLE, a superuser or a role that bypasses row
/// security, and answers as owner of neither the database, nor a schema, nor a table, by owning it or by belonging to its owner.
/// </summary>
public sealed class PrivilegeCheck : IStartupCheck
{
    private const string Query = """
        SELECT current_user::text AS role,
            (SELECT s.rolname::text FROM pg_roles s WHERE s.rolsuper AND pg_has_role(s.oid, 'MEMBER')
                ORDER BY s.rolname <> current_user, s.rolname LIMIT 1) AS superuser_role,
            (SELECT s.rolname::text FROM pg_roles s WHERE s.rolbypassrls AND pg_has_role(s.oid, 'MEMBER')
                ORDER BY s.rolname <> current_user, s.rolname LIMIT 1) AS bypass_role,
            (SELECT d.datname::text FROM pg_database d
                WHERE d.datname = current_database() AND pg_has_role(d.datdba, 'MEMBER')) AS owned_database,
            (SELECT min(n.nspname::text) FROM pg_namespace n WHERE pg_has_role(n.nspowner, 'MEMBER')) AS owned_schema,
            (SELECT min(c.oid::regclass::text) FROM pg_class c
                WHERE c.relkind IN ('r', 'p') AND pg_has_role(c.relowner, 'MEMBER')) AS owned_table
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
        { SuperuserRole: { } held } => Escalation(role.Role, held, "SUPERUSER", "desabilita gatilho e política de linha"),
        { BypassRole: { } held } => Escalation(role.Role, held, "BYPASSRLS", "escapa da política de linha"),
        { Database: { } database } => Owner(role.Role, "do banco", "DATABASE", database),
        { Schema: { } schema } => Owner(role.Role, "do esquema", "SCHEMA", schema),
        { Table: { } table } => Owner(role.Role, "da tabela", "TABLE", table),
        _ => Finding.Holds($"o papel '{role.Role}' não é superusuário, não tem BYPASSRLS e não é dono do banco, de esquema nem de tabela"),
    };

    private static Finding Escalation(string role, string held, string attribute, string effect) =>
        Finding.Missing(
            held == role
                ? $"o papel '{role}' tem {attribute}, que {effect}: execute ALTER ROLE {role} NO{attribute}"
                : $"o papel '{role}' pode assumir, com SET ROLE, o papel '{held}', que tem {attribute} e {effect}: "
                    + $"execute REVOKE {held} FROM {role}");

    private static Finding Owner(string role, string what, string kind, string name) =>
        Finding.Missing(
            $"o papel '{role}' responde como dono {what} {name}, por posse ou por pertença ao papel dono: transfira a posse com "
            + $"ALTER {kind} {name} OWNER TO <papel dono>, ou revogue a pertença com REVOKE <papel dono> FROM {role}");

    private static Task<Finding> ReadAsync(NpgsqlConnection connection, CancellationToken cancellationToken) =>
        ServerProbe.ReadRowAsync(connection, server => new NpgsqlCommand(Query, server), Read, cancellationToken);

    private static Finding Read(IDataRecord row) =>
        Judge(new RoleReading(
            (string)row["role"],
            row["superuser_role"] as string,
            row["bypass_role"] as string,
            row["owned_database"] as string,
            row["owned_schema"] as string,
            row["owned_table"] as string));

    /// <summary>What the server says about the role the application connected as.</summary>
    internal sealed record RoleReading(string Role, string? SuperuserRole, string? BypassRole, string? Database, string? Schema, string? Table);
}
