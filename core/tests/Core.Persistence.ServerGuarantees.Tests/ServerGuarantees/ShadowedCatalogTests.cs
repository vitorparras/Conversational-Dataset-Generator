using Core.Persistence.Migrations;
using Core.Persistence.ServerGuarantees;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// With a schema ahead of pg_catalog on the search path, ready to say that every guarantee holds, each check still reads the catalog itself and accuses
/// the guarantee that is missing. The tables, the views and the functions are named with pg_catalog in the text of the readings; the operators and the types
/// are not, and the reading runs with the path set to the catalog.
/// </summary>
[Trait("Category", "Database")]
public sealed class ShadowedCatalogTests
{
    [DatabaseFact]
    public async Task SessionThatDoesNotPinTheSearchPathSeesTheShadowInPlaceOfTheCatalog()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        await using var dataSource = await ShadowedSession.ShadowAsync(database);
        await using var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_class", connection);

        var seen = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0L, seen);
    }

    [DatabaseFact]
    public async Task ColumnRevokeCheckStillAccusesTheTableWhoseColumnGrantIsVoided()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        await database.RunAsOwnerAsync(
            "CREATE TABLE public.zqx_voided (a integer)",
            "GRANT UPDATE (a) ON public.zqx_voided TO app",
            "GRANT UPDATE ON public.zqx_voided TO app");

        var result = await ShadowedSession.CheckAsync(database, dataSource => new ColumnRevokeCheck(dataSource));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("o papel 'app' tem UPDATE na tabela public.zqx_voided inteira", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task PgAuditCheckStillAccusesTheExtensionThatIsNotInstalled()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        await ShadowedSession.RunAsSuperuserAsync(database, "DROP EXTENSION pgaudit");

        var result = await ShadowedSession.CheckAsync(database, dataSource => new PgAuditCheck(dataSource));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.StartsWith("a extensão pgaudit não está instalada no banco", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task PrivilegeCheckStillAccusesTheTableTheRoleOwns()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        await ShadowedSession.RunAsSuperuserAsync(database, "CREATE TABLE public.zqx_owned (a integer)", "ALTER TABLE public.zqx_owned OWNER TO app");

        var result = await ShadowedSession.CheckAsync(database, dataSource => new PrivilegeCheck(dataSource));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.StartsWith("o papel 'app' responde como dono da tabela public.zqx_owned", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task RlsForceCheckStillAccusesThePolicyThatDoesNotApplyAgainstTheOwner()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        await database.RunAsOwnerAsync(
            "CREATE TABLE public.zqx_open (a integer)",
            "ALTER TABLE public.zqx_open ENABLE ROW LEVEL SECURITY",
            "CREATE POLICY zqx_all ON public.zqx_open USING (true)");

        var result = await ShadowedSession.CheckAsync(database, dataSource => new RlsForceCheck(dataSource));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.StartsWith("a tabela public.zqx_open tem política de linha que não vale contra o dono", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task DerivationLabelCheckStillAccusesTheLabelInUseThatIsNotProvided()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await database.RecordLabelsAsync(["zqx-dominio-a"]);

        var result = await ShadowedSession.CheckAsync(database, dataSource => new DerivationLabelCheck(dataSource, LabelsSource.Providing("zqx-dominio-b")));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("zqx-dominio-a", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task SerializableCheckStillReadsTheIsolationTheServerOpenedNotTheOneTheShadowSays()
    {
        await using var database = await ScratchDatabase.CreateAsync();

        var result = await ShadowedSession.CheckAsync(database, dataSource => new SerializableCheck(dataSource));

        Assert.Equal(CheckStatus.Passed, result.Status);
        Assert.Equal("o servidor abre transação SERIALIZABLE para o papel da aplicação", result.Message);
    }
}
