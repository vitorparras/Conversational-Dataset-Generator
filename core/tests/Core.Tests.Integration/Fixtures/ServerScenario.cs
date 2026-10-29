using System.Diagnostics;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Tests.Integration.Fixtures;

/// <summary>
/// What the superuser arranges for one test, in one of a few fixed shapes, and takes back on dispose. The roles it makes
/// never log in: a check runs as one through <see cref="RunAs"/>, which sets the role when the session starts.
/// </summary>
internal sealed class ServerScenario : IAsyncDisposable
{
    public const string Role = "zqx_role";

    private readonly string superuserConnectionString;
    private readonly Shape shape;

    private ServerScenario(string superuserConnectionString, Shape shape)
    {
        this.superuserConnectionString = superuserConnectionString;
        this.shape = shape;
    }

    internal enum Shape
    {
        /// <summary>zqx_role belongs to zqx_super, a superuser role it can SET ROLE to.</summary>
        MemberOfASuperuserRole = 0,

        /// <summary>zqx_role belongs to owner, the role that owns the database and its schema.</summary>
        MemberOfTheOwnerRole = 1,

        /// <summary>zqx_role belongs to zqx_tables, which owns the table zqx_owned and nothing else.</summary>
        MemberOfATableOwner = 2,

        /// <summary>PUBLIC can no longer read pg_policy.</summary>
        UnreadablePolicyCatalog = 3,

        /// <summary>zqx_role belongs to zqx_bypass, a role that bypasses row security.</summary>
        MemberOfABypassRlsRole = 4,
    }

    public static async Task<ServerScenario> ArrangeAsync(string superuserConnectionString, Shape shape)
    {
        var sql = shape switch
        {
            Shape.MemberOfASuperuserRole => "CREATE ROLE zqx_role NOLOGIN; CREATE ROLE zqx_super SUPERUSER NOLOGIN; GRANT zqx_super TO zqx_role",
            Shape.MemberOfTheOwnerRole => "CREATE ROLE zqx_role NOLOGIN; GRANT owner TO zqx_role",
            Shape.MemberOfATableOwner =>
                "CREATE ROLE zqx_role NOLOGIN; CREATE ROLE zqx_tables NOLOGIN; GRANT zqx_tables TO zqx_role; "
                + "CREATE TABLE zqx_owned (a integer); ALTER TABLE zqx_owned OWNER TO zqx_tables",
            Shape.UnreadablePolicyCatalog => "REVOKE SELECT ON pg_catalog.pg_policy FROM PUBLIC",
            Shape.MemberOfABypassRlsRole => "CREATE ROLE zqx_role NOLOGIN; CREATE ROLE zqx_bypass BYPASSRLS NOLOGIN; GRANT zqx_bypass TO zqx_role",
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };
        await using var connection = await Sql.OpenAsync(superuserConnectionString);
        await using var command = new NpgsqlCommand(sql, connection);
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return new ServerScenario(superuserConnectionString, shape);
    }

    /// <summary>The superuser's connection string, its session running as the given role.</summary>
    public static string RunAs(string superuserConnectionString, string role) =>
        new NpgsqlConnectionStringBuilder(superuserConnectionString) { Options = $"-c role={role}" }.ConnectionString;

    public async ValueTask DisposeAsync()
    {
        var sql = shape switch
        {
            Shape.MemberOfASuperuserRole => "DROP ROLE zqx_role, zqx_super",
            Shape.MemberOfTheOwnerRole => "DROP ROLE zqx_role",
            Shape.MemberOfATableOwner => "DROP TABLE zqx_owned; DROP ROLE zqx_role, zqx_tables",
            Shape.UnreadablePolicyCatalog => "GRANT SELECT ON pg_catalog.pg_policy TO PUBLIC",
            Shape.MemberOfABypassRlsRole => "DROP ROLE zqx_role, zqx_bypass",
            _ => throw new UnreachableException(),
        };
        await using var connection = await Sql.OpenAsync(superuserConnectionString);
        await using var command = new NpgsqlCommand(sql, connection);
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
