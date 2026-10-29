using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Tests.Integration.Fixtures;

/// <summary>A table the role owner creates for one test in one of a few fixed shapes; every such table is dropped on dispose.</summary>
internal sealed class OwnedTable : IAsyncDisposable
{
    private readonly string ownerConnectionString;

    private OwnedTable(string ownerConnectionString)
    {
        this.ownerConnectionString = ownerConnectionString;
    }

    internal enum Shape
    {
        /// <summary>zqx_forced: row security enabled and forced.</summary>
        ForcedRowSecurity = 0,

        /// <summary>zqx_unforced: row security enabled, not forced.</summary>
        UnforcedRowSecurity = 1,

        /// <summary>zqx_policy_off: a row policy on a table whose row security is off.</summary>
        PolicyWithoutRowSecurity = 2,

        /// <summary>zqx_by_column: UPDATE granted to app on one column only, INSERT on the table.</summary>
        ColumnGrant = 3,

        /// <summary>zqx_voided: UPDATE granted to app on one column and to PUBLIC on the whole table.</summary>
        VoidedColumnGrant = 4,
    }

    public static async Task<OwnedTable> CreateAsync(string ownerConnectionString, Shape shape)
    {
        var sql = shape switch
        {
            Shape.ForcedRowSecurity => "CREATE TABLE zqx_forced (a integer); ALTER TABLE zqx_forced ENABLE ROW LEVEL SECURITY, FORCE ROW LEVEL SECURITY",
            Shape.UnforcedRowSecurity => "CREATE TABLE zqx_unforced (a integer); ALTER TABLE zqx_unforced ENABLE ROW LEVEL SECURITY",
            Shape.PolicyWithoutRowSecurity => "CREATE TABLE zqx_policy_off (a integer); CREATE POLICY zqx_policy ON zqx_policy_off USING (true)",
            Shape.ColumnGrant => "CREATE TABLE zqx_by_column (a integer); GRANT UPDATE (a) ON zqx_by_column TO app; GRANT INSERT ON zqx_by_column TO app",
            Shape.VoidedColumnGrant => "CREATE TABLE zqx_voided (a integer); GRANT UPDATE (a) ON zqx_voided TO app; GRANT UPDATE ON zqx_voided TO PUBLIC",
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };
        await using var connection = await Sql.OpenAsync(ownerConnectionString);
        await using var command = new NpgsqlCommand(sql, connection);
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return new OwnedTable(ownerConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        await using var connection = await Sql.OpenAsync(ownerConnectionString);
        await using var command = new NpgsqlCommand("DROP TABLE IF EXISTS zqx_forced, zqx_unforced, zqx_policy_off, zqx_by_column, zqx_voided", connection);
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
