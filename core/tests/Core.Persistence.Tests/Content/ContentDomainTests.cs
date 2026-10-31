using Core.Persistence.Content;
using Core.Persistence.Migrations;
using Core.Persistence.ServerGuarantees;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// The domain is the axis of every content table, and the database holds it: a session that declares none sees no row, one that declares
/// a domain sees that domain only, and the owner of the table is no exception. The table is made by the mold, in a database of its own.
/// </summary>
[Trait("Category", "Database")]
public sealed class ContentDomainTests
{
    private const string Table = "content.zqx_item";

    [DatabaseFact]
    public async Task SessionThatDeclaresNoDomainSeesNoRowOfAContentTable()
    {
        await using var database = await WithTwoDomainsAsync();

        var seen = await CountWithNoDomainAsync(database.AppConnectionString);

        Assert.Equal(0, seen);
    }

    [DatabaseFact]
    public async Task DeclaredDomainIsNullWhenNobodyDeclaredOne()
    {
        await using var database = await WithTwoDomainsAsync();
        await using var connection = await Sql.OpenAsync(database.AppConnectionString);
        await using var command = new NpgsqlCommand("SELECT public.declared_domain()", connection);

        var declared = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DBNull.Value, declared);
    }

    [DatabaseFact]
    public async Task SessionThatDeclaresADomainSeesItsRowsAndNoneOfTheOther()
    {
        await using var database = await WithTwoDomainsAsync();

        var first = await BodiesAsync(database.AppConnectionString, domain: 1);
        var second = await BodiesAsync(database.AppConnectionString, domain: 2);

        Assert.Equal(["a1", "a2"], first);
        Assert.Equal(["b1"], second);
    }

    [DatabaseFact]
    public async Task WritingARowOfAnotherDomainIsRefusedByTheDatabase()
    {
        await using var database = await WithTwoDomainsAsync();
        await using var dataSource = NpgsqlDataSource.Create(database.AppConnectionString);
        await using var transaction = await ContentTransaction.BeginAsync(dataSource, 1, TestContext.Current.CancellationToken);
        // nosemgrep: sql-concatenation-csharp interpolates only the name of the table this test creates, a constant.
        await using var insert = transaction.CreateCommand($"INSERT INTO {Table} (domain_id, body) VALUES (2, 'x')");

        var refused = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

        Assert.Equal("42501", refused.SqlState);
    }

    [DatabaseFact]
    public async Task ContentTableNamedWithoutItsSchemaIsRefusedForTheApplicationAndForTheOwner()
    {
        await using var database = await WithTwoDomainsAsync();

        foreach (var connectionString in new[] { database.AppConnectionString, database.OwnerConnectionString })
        {
            await using var connection = await Sql.OpenAsync(connectionString);
            await using var command = new NpgsqlCommand("SELECT count(*) FROM zqx_item", connection);

            var refused = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteScalarAsync(TestContext.Current.CancellationToken));

            Assert.Equal(PostgresErrorCodes.UndefinedTable, refused.SqlState);
        }
    }

    [DatabaseFact]
    public async Task NoSettingOfTheRoleOrOfTheDatabaseGivesTheContentSchemaAPlaceOnTheSearchPath()
    {
        await using var database = await WithTwoDomainsAsync();
        await using var connection = await Sql.OpenAsync(database.AppConnectionString);
        await using var command = new NpgsqlCommand(
            """
            SELECT count(*) FROM pg_db_role_setting s, unnest(s.setconfig) AS setting
            WHERE s.setdatabase IN (0, (SELECT oid FROM pg_database WHERE datname = current_database()))
                AND setting ILIKE 'search_path=%content%'
            """,
            connection);

        var set = (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;

        Assert.Equal(0, set);
    }

    [DatabaseFact]
    public async Task TheDeclarationEndsWithTheTransaction()
    {
        await using var database = await WithTwoDomainsAsync();
        _ = await BodiesAsync(database.AppConnectionString, domain: 1);

        var after = await CountWithNoDomainAsync(database.AppConnectionString);

        Assert.Equal(0, after);
    }

    [DatabaseFact]
    public async Task OwnerThatDeclaresNoDomainSeesNoRowEither()
    {
        await using var database = await WithTwoDomainsAsync();

        var seen = await CountWithNoDomainAsync(database.OwnerConnectionString);

        Assert.Equal(0, seen);
    }

    [DatabaseFact]
    public async Task OwnerThatDeclaresADomainSeesThatDomainOnly()
    {
        await using var database = await WithTwoDomainsAsync();

        var seen = await BodiesAsync(database.OwnerConnectionString, domain: 2);

        Assert.Equal(["b1"], seen);
    }

    [DatabaseFact]
    public async Task TableMadeByTheMoldPassesTheCheck()
    {
        await using var database = await WithTwoDomainsAsync();

        var result = await CheckAsync(database);

        Assert.Equal(CheckStatus.Passed, result.Status);
    }

    [DatabaseFact]
    public async Task ContentTableWithoutTheDomainColumnIsNamed()
    {
        await using var database = await WithTwoDomainsAsync();
        await database.RunAsOwnerAsync("CREATE TABLE content.zqx_loose (id bigint PRIMARY KEY, body text)");

        var result = await CheckAsync(database);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("content.zqx_loose", result.Message, StringComparison.Ordinal);
        Assert.Contains("domain_id", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ContentTableWithTheColumnButNoPolicyIsNamed()
    {
        await using var database = await WithTwoDomainsAsync();
        await database.RunAsOwnerAsync(
            "CREATE TABLE content.zqx_open (id bigint PRIMARY KEY, domain_id integer NOT NULL REFERENCES public.domain (id), body text)");

        var result = await CheckAsync(database);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("content.zqx_open", result.Message, StringComparison.Ordinal);
        Assert.Contains("política de linha", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ContentTableWhoseColumnMayBeNullIsNamed()
    {
        await using var database = await WithTwoDomainsAsync();
        await database.RunAsOwnerAsync(
            "CREATE TABLE content.zqx_nullable (id bigint PRIMARY KEY, domain_id integer, body text)",
            "ALTER TABLE content.zqx_nullable ENABLE ROW LEVEL SECURITY",
            "ALTER TABLE content.zqx_nullable FORCE ROW LEVEL SECURITY",
            "CREATE POLICY p ON content.zqx_nullable USING (domain_id = public.declared_domain())");

        var result = await CheckAsync(database);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("content.zqx_nullable", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ContentTableWhoseDomainColumnHasNoForeignKeyToTheDomainRegistryIsNamed()
    {
        await using var database = await WithTwoDomainsAsync();
        await database.RunAsOwnerAsync(
            "CREATE TABLE content.zqx_untied (id bigint PRIMARY KEY, domain_id integer NOT NULL, body text)",
            "ALTER TABLE content.zqx_untied ENABLE ROW LEVEL SECURITY",
            "ALTER TABLE content.zqx_untied FORCE ROW LEVEL SECURITY",
            "CREATE POLICY p ON content.zqx_untied USING (domain_id = public.declared_domain()) WITH CHECK (domain_id = public.declared_domain())");

        var result = await CheckAsync(database);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("content.zqx_untied", result.Message, StringComparison.Ordinal);
        Assert.Contains("chave estrangeira", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ContentTableWhoseForeignKeyToTheDomainRegistryWasNeverValidatedIsNamed()
    {
        await using var database = await WithTwoDomainsAsync();
        await database.RunAsOwnerAsync(
            "CREATE TABLE content.zqx_unchecked (id bigint PRIMARY KEY, domain_id integer NOT NULL, body text)",
            "ALTER TABLE content.zqx_unchecked ADD CONSTRAINT zqx_unchecked_domain FOREIGN KEY (domain_id) REFERENCES public.domain (id) NOT VALID",
            "ALTER TABLE content.zqx_unchecked ENABLE ROW LEVEL SECURITY",
            "ALTER TABLE content.zqx_unchecked FORCE ROW LEVEL SECURITY",
            "CREATE POLICY p ON content.zqx_unchecked USING (domain_id = public.declared_domain()) WITH CHECK (domain_id = public.declared_domain())");

        var result = await CheckAsync(database);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("content.zqx_unchecked", result.Message, StringComparison.Ordinal);
        Assert.Contains("VALIDATE CONSTRAINT", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ContentTableWhoseForeignKeyToTheDomainRegistryIsValidatedAfterwardsPassesTheCheck()
    {
        await using var database = await WithTwoDomainsAsync();
        await database.RunAsOwnerAsync(
            "CREATE TABLE content.zqx_late (id bigint PRIMARY KEY, domain_id integer NOT NULL, body text)",
            "ALTER TABLE content.zqx_late ADD CONSTRAINT zqx_late_domain FOREIGN KEY (domain_id) REFERENCES public.domain (id) NOT VALID",
            "ALTER TABLE content.zqx_late VALIDATE CONSTRAINT zqx_late_domain",
            "ALTER TABLE content.zqx_late ENABLE ROW LEVEL SECURITY",
            "ALTER TABLE content.zqx_late FORCE ROW LEVEL SECURITY",
            "CREATE POLICY p ON content.zqx_late USING (domain_id = public.declared_domain()) WITH CHECK (domain_id = public.declared_domain())");

        var result = await CheckAsync(database);

        Assert.Equal(CheckStatus.Passed, result.Status);
    }

    [DatabaseFact]
    public async Task ContentTableTiedToAnotherTableThanTheDomainRegistryIsNamed()
    {
        await using var database = await WithTwoDomainsAsync();
        await database.RunAsOwnerAsync(
            "CREATE TABLE content.zqx_elsewhere (id bigint PRIMARY KEY, domain_id integer NOT NULL REFERENCES content.zqx_item (id), body text)",
            "ALTER TABLE content.zqx_elsewhere ENABLE ROW LEVEL SECURITY",
            "ALTER TABLE content.zqx_elsewhere FORCE ROW LEVEL SECURITY",
            "CREATE POLICY p ON content.zqx_elsewhere USING (domain_id = public.declared_domain()) WITH CHECK (domain_id = public.declared_domain())");

        var result = await CheckAsync(database);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("content.zqx_elsewhere", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ContentTableWithThePolicyButNoForceIsNamedWithTheCommandsThatFixIt()
    {
        await using var database = await WithTwoDomainsAsync();
        await database.RunAsOwnerAsync("ALTER TABLE content.zqx_item NO FORCE ROW LEVEL SECURITY");

        var result = await CheckAsync(database);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("ALTER TABLE content.zqx_item FORCE ROW LEVEL SECURITY", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ContentTableWithAPermissivePolicyThatLetsEverythingThroughIsNamed()
    {
        await using var database = await WithTwoDomainsAsync();
        await database.RunAsOwnerAsync($"CREATE POLICY extra ON {Table} USING (true)");

        var result = await CheckAsync(database);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains(Table, result.Message, StringComparison.Ordinal);
        Assert.Contains("permissiva", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ContentTableWithAnInsertPolicyThatChecksNothingIsNamed()
    {
        await using var database = await WithTwoDomainsAsync();
        await database.RunAsOwnerAsync($"CREATE POLICY ins ON {Table} FOR INSERT WITH CHECK (true)");

        var result = await CheckAsync(database);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains(Table, result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task PolicyThatRepeatsTheDomainRuleOrOnlyRestrictsDoesNotOpenTheTable()
    {
        await using var database = await WithTwoDomainsAsync();
        await database.RunAsOwnerAsync(
            $"CREATE POLICY again ON {Table} FOR SELECT USING ((domain_id = public.declared_domain()))",
            $"CREATE POLICY narrow ON {Table} AS RESTRICTIVE USING (true)");

        var result = await CheckAsync(database);

        Assert.Equal(CheckStatus.Passed, result.Status);
    }

    [DatabaseFact]
    public async Task MaterializedViewInTheContentSchemaIsNamed()
    {
        await using var database = await WithTwoDomainsAsync();
        await database.RunAsOwnerAsync($"CREATE MATERIALIZED VIEW content.zqx_snap AS SELECT * FROM {Table}");

        var result = await CheckAsync(database);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("content.zqx_snap", result.Message, StringComparison.Ordinal);
        Assert.Contains("não é tabela", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ViewInTheContentSchemaIsNamed()
    {
        await using var database = await WithTwoDomainsAsync();
        await database.RunAsOwnerAsync($"CREATE VIEW content.zqx_look AS SELECT * FROM {Table}");

        var result = await CheckAsync(database);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("content.zqx_look", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task NoQueryTheSystemExposesReturnsAnItemWithNoDomainDeclared()
    {
        await using var database = await WithTwoDomainsAsync();
        await using var connection = await Sql.OpenAsync(database.AppConnectionString);

        foreach (var (name, statement) in ContentQueries.All)
        {
            await using var command = new NpgsqlCommand(statement, connection);
            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            Assert.False(await reader.ReadAsync(TestContext.Current.CancellationToken), $"the query {name} returned an item with no domain declared");
        }
    }

    [Fact]
    public async Task DomainThatIsNotPositiveIsRefusedBeforeAnyConnectionOpens()
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => ContentTransaction.BeginAsync(dataSource, 0, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MissingDataSourceIsRefusedBeforeAnyConnectionOpens()
    {
        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            () => ContentTransaction.BeginAsync(null!, 1, TestContext.Current.CancellationToken));
    }

    [DatabaseFact]
    public async Task ConnectionIsReleasedWhenTheDeclarationFails()
    {
        await using var database = await WithTwoDomainsAsync();
        await using var dataSource = SingleConnectionSource(database.AppConnectionString);

        _ = await Assert.ThrowsAsync<PostgresException>(
            () => ContentTransaction.OpenAsync(dataSource, 1, "SELECT 1 / 0 WHERE @domain IS NOT NULL", TestContext.Current.CancellationToken));

        await using var next = await ContentTransaction.BeginAsync(dataSource, 1, TestContext.Current.CancellationToken);
        Assert.NotNull(next);
    }

    [DatabaseFact]
    public async Task ConnectionIsReleasedWhenTheTransactionIsDisposed()
    {
        await using var database = await WithTwoDomainsAsync();
        await using var dataSource = SingleConnectionSource(database.AppConnectionString);
        var first = await ContentTransaction.BeginAsync(dataSource, 1, TestContext.Current.CancellationToken);
        await first.DisposeAsync();

        await using var next = await ContentTransaction.BeginAsync(dataSource, 1, TestContext.Current.CancellationToken);
        Assert.NotNull(next);
    }

    [DatabaseFact]
    public async Task WriteNotCommittedIsGoneWhenTheTransactionIsDisposed()
    {
        await using var database = await WithTwoDomainsAsync();
        await using (var dataSource = NpgsqlDataSource.Create(database.OwnerConnectionString))
        {
            var writing = await ContentTransaction.BeginAsync(dataSource, 1, TestContext.Current.CancellationToken);
            // nosemgrep: sql-concatenation-csharp interpolates only the name of the table this test creates, a constant.
            await using var insert = writing.CreateCommand($"INSERT INTO {Table} (domain_id, body) VALUES (1, 'a3')");
            _ = await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            await writing.DisposeAsync();
        }

        var seen = await BodiesAsync(database.OwnerConnectionString, domain: 1);

        Assert.Equal(["a1", "a2"], seen);
    }

    private static NpgsqlDataSource SingleConnectionSource(string connectionString) =>
        NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(connectionString) { Pooling = true, MaxPoolSize = 1, Timeout = 3 }.ConnectionString);

    private static async Task<ScratchDatabase> WithTwoDomainsAsync()
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await database.RunAsOwnerAsync(
            "INSERT INTO public.domain (name) VALUES ('zqx-domain-a'), ('zqx-domain-b')",
            "SELECT public.create_content_table('zqx_item', 'body text NOT NULL')",
            $"GRANT SELECT, INSERT ON {Table} TO app");
        await InsertAsync(database.OwnerConnectionString, domain: 1, "a1", "a2");
        await InsertAsync(database.OwnerConnectionString, domain: 2, "b1");
        return database;
    }

    private static async Task InsertAsync(string connectionString, int domain, params string[] bodies)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var transaction = await ContentTransaction.BeginAsync(dataSource, domain, TestContext.Current.CancellationToken);
        foreach (var body in bodies)
        {
            // nosemgrep: sql-concatenation-csharp interpolates a table constant and literals the test itself passes.
            await using var insert = transaction.CreateCommand($"INSERT INTO {Table} (domain_id, body) VALUES ({domain}, '{body}')");
            _ = await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<IReadOnlyList<string>> BodiesAsync(string connectionString, int domain)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var transaction = await ContentTransaction.BeginAsync(dataSource, domain, TestContext.Current.CancellationToken);
        // nosemgrep: sql-concatenation-csharp interpolates only the name of the table this test creates, a constant.
        await using var select = transaction.CreateCommand($"SELECT body FROM {Table} ORDER BY body");
        await using var reader = await select.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var bodies = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            bodies.Add(reader.GetString(0));
        }

        return bodies;
    }

    private static async Task<long> CountWithNoDomainAsync(string connectionString)
    {
        await using var connection = await Sql.OpenAsync(connectionString);
        // nosemgrep: sql-concatenation-csharp interpolates only the name of the table this test creates, a constant.
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM {Table}", connection);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static Task<CheckResult> CheckAsync(ScratchDatabase database) =>
        AppStartup.CheckAsync(database, dataSource => new RlsForceCheck(dataSource));
}
