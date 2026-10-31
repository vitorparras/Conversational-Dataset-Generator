using Core.Persistence.Tests.Fixtures;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// A database whose search path puts a schema of the owner ahead of pg_catalog, as a database setting does for every session of the application's role.
/// The schema answers the way a server that kept every guarantee would: views of the same name as the catalog's say nothing is missing, functions of the same
/// name say what suits, and an operator and a type of the same name as the ones the readings use either turn the comparison or break the cast.
/// </summary>
internal static class ShadowedSession
{
    private static readonly string[] Shadow =
    [
        "CREATE SCHEMA zqx_shadow",
        "CREATE VIEW zqx_shadow.pg_class AS SELECT * FROM pg_catalog.pg_class WHERE false",
        "CREATE VIEW zqx_shadow.pg_attribute AS SELECT attrelid, attname, attnum, attacl, attnotnull, attisdropped FROM pg_catalog.pg_attribute WHERE false",
        "CREATE VIEW zqx_shadow.pg_policy AS SELECT * FROM pg_catalog.pg_policy WHERE false",
        "CREATE VIEW zqx_shadow.pg_namespace AS SELECT * FROM pg_catalog.pg_namespace WHERE false",
        "CREATE VIEW zqx_shadow.pg_database AS SELECT * FROM pg_catalog.pg_database WHERE false",
        "CREATE VIEW zqx_shadow.pg_roles AS SELECT oid, rolname, false AS rolsuper, false AS rolbypassrls FROM pg_catalog.pg_roles",
        "CREATE VIEW zqx_shadow.pg_extension AS SELECT 'pgaudit'::name AS extname, '9.9'::text AS extversion",
        "CREATE VIEW zqx_shadow.pg_settings AS SELECT 'pgaudit.log'::text AS name, 'write'::text AS setting, 'superuser'::text AS context",
        "CREATE FUNCTION zqx_shadow.has_table_privilege(oid, text) RETURNS boolean LANGUAGE sql AS 'SELECT false'",
        "CREATE FUNCTION zqx_shadow.pg_has_role(oid, text) RETURNS boolean LANGUAGE sql AS 'SELECT false'",
        "CREATE FUNCTION zqx_shadow.current_setting(text) RETURNS text LANGUAGE sql AS 'SELECT ''read committed'''",
        "CREATE FUNCTION zqx_shadow.differ(oid, oid) RETURNS boolean LANGUAGE sql IMMUTABLE AS 'SELECT false'",
        "CREATE FUNCTION zqx_shadow.coincide(name, name) RETURNS boolean LANGUAGE sql IMMUTABLE AS 'SELECT true'",
        "CREATE OPERATOR zqx_shadow.= (LEFTARG = oid, RIGHTARG = oid, FUNCTION = zqx_shadow.differ)",
        "CREATE OPERATOR zqx_shadow.= (LEFTARG = name, RIGHTARG = name, FUNCTION = zqx_shadow.coincide)",
        "CREATE TYPE zqx_shadow.regclass AS ENUM ('zqx')",
        "CREATE TYPE zqx_shadow.text AS ENUM ('zqx')",
        "GRANT USAGE ON SCHEMA zqx_shadow TO app",
        "GRANT SELECT ON ALL TABLES IN SCHEMA zqx_shadow TO app",
    ];

    /// <summary>The result of the check for the application's role in a database the test set up and this then shadowed.</summary>
    public static async Task<CheckResult> CheckAsync(ScratchDatabase database, Func<NpgsqlDataSource, IStartupCheck> check)
    {
        await using var dataSource = await ShadowAsync(database);
        return await check(dataSource).RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The data source of the application's role over the database once the shadow stands ahead of the catalog on the search path. The driver reads its
    /// types through the catalog when it first connects, so a connection is opened and given back before the shadow exists, and the data source keeps what it read.
    /// </summary>
    public static async Task<NpgsqlDataSource> ShadowAsync(ScratchDatabase database)
    {
        var dataSource = NpgsqlDataSource.Create(database.AppConnectionString);
        await (await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken)).DisposeAsync();
        await database.RunAsOwnerAsync(Shadow);
        var name = new NpgsqlConnectionStringBuilder(database.OwnerConnectionString).Database;
        await database.RunAsOwnerAsync($"ALTER DATABASE {name} SET search_path = zqx_shadow, pg_catalog");
        return dataSource;
    }

    /// <summary>Runs the statements in the database as the superuser, which the owner cannot do: give a table to the application's role or drop an extension.</summary>
    public static async Task RunAsSuperuserAsync(ScratchDatabase database, params string[] statements)
    {
        var shared = await PostgresFixture.SharedAsync();
        var inScratch = new NpgsqlConnectionStringBuilder(shared.SuperuserConnectionString)
        {
            Database = new NpgsqlConnectionStringBuilder(database.OwnerConnectionString).Database,
            Pooling = false,
        };
        await using var connection = await Sql.OpenAsync(inScratch.ConnectionString);
        await using var batch = new NpgsqlBatch(connection);
        foreach (var statement in statements)
        {
            batch.BatchCommands.Add(new NpgsqlBatchCommand(statement));
        }

        _ = await batch.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
