using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Tests.Integration.Database;

/// <summary>The built image, running: pgaudit is loaded, the owner role owns the schema without escaping row security, and the application role owns nothing.</summary>
[Trait("Category", "Database")]
public sealed class DatabaseRolesTests
{
    /// <summary>
    /// Only the loaded library defines pgaudit.log, and pgaudit refuses to load from anywhere but shared_preload_libraries, which
    /// only superusers may read. A placeholder set by a connection option or by the configuration file answers to
    /// current_setting but never shows in pg_settings.
    /// </summary>
    [DatabaseFact]
    public async Task ServerLoadsPgauditAtStartup()
    {
        var database = await PostgresFixture.SharedAsync();
        await using var connection = await Sql.OpenAsync(database.AppConnectionString);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_settings WHERE name = 'pgaudit.log'", connection);

        var defined = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1L, defined);
    }

    [DatabaseFact]
    public async Task PgauditExtensionIsInstalledInTheDatabase()
    {
        var database = await PostgresFixture.SharedAsync();
        await using var connection = await Sql.OpenAsync(database.AppConnectionString);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_extension WHERE extname = 'pgaudit'", connection);

        var installed = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1L, installed);
    }

    [DatabaseFact]
    public async Task ApplicationRoleOwnsNoSchema()
    {
        var database = await PostgresFixture.SharedAsync();
        await using var connection = await Sql.OpenAsync(database.AppConnectionString);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_namespace WHERE nspowner = (SELECT oid FROM pg_roles WHERE rolname = current_user)", connection);

        var owned = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0L, owned);
    }

    [DatabaseFact]
    public async Task ApplicationRoleOwnsNoDatabase()
    {
        var database = await PostgresFixture.SharedAsync();
        await using var connection = await Sql.OpenAsync(database.AppConnectionString);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datdba = (SELECT oid FROM pg_roles WHERE rolname = current_user)", connection);

        var owned = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0L, owned);
    }

    [DatabaseFact]
    public async Task ApplicationRoleHoldsNoAdministrativeAttribute()
    {
        var database = await PostgresFixture.SharedAsync();
        await using var connection = await Sql.OpenAsync(database.AppConnectionString);
        await using var command = new NpgsqlCommand("SELECT rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls FROM pg_roles WHERE rolname = current_user", connection);

        var privileged = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.False(Assert.IsType<bool>(privileged));
    }

    [DatabaseFact]
    public async Task ApplicationRoleCannotCreateInThePublicSchema()
    {
        var database = await PostgresFixture.SharedAsync();
        await using var connection = await Sql.OpenAsync(database.AppConnectionString);
        await using var command = new NpgsqlCommand("SELECT has_schema_privilege('public', 'CREATE')", connection);

        var mayCreate = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.False(Assert.IsType<bool>(mayCreate));
    }

    [DatabaseFact]
    public async Task ApplicationRoleIsMemberOfNoRole()
    {
        var database = await PostgresFixture.SharedAsync();
        await using var connection = await Sql.OpenAsync(database.AppConnectionString);
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM pg_auth_members WHERE member = (SELECT oid FROM pg_roles WHERE rolname = current_user)",
            connection);

        var memberships = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0L, memberships);
    }

    [DatabaseFact]
    public async Task OwnerRoleHoldsNoSuperuserNorBypassRls()
    {
        var database = await PostgresFixture.SharedAsync();
        await using var connection = await Sql.OpenAsync(database.OwnerConnectionString);
        await using var command = new NpgsqlCommand("SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = current_user", connection);

        var escapesRowSecurity = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.False(Assert.IsType<bool>(escapesRowSecurity));
    }

    [DatabaseFact]
    public async Task OwnerRoleOwnsThePublicSchema()
    {
        var database = await PostgresFixture.SharedAsync();
        await using var connection = await Sql.OpenAsync(database.OwnerConnectionString);
        await using var command = new NpgsqlCommand("SELECT pg_get_userbyid(nspowner) FROM pg_namespace WHERE nspname = 'public'", connection);

        var owner = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.Equal("owner", owner);
    }
}
