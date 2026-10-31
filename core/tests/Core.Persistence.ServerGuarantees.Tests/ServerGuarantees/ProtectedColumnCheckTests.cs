using Core.Persistence.Content;
using Core.Persistence.Migrations;
using Core.Persistence.ServerGuarantees;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Startup;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// A column of the content schema is either sealed, with the mark of the purpose of its key, or listed as stored in clear with the type of its kind:
/// the one that is neither is named, because it would write material in clear and nothing else would notice.
/// </summary>
public sealed class ProtectedColumnCheckTests
{
    [Fact]
    public async Task UnreachableServerIsARefusalNamingTheCheck()
    {
        await using var server = new UnreachableServer();

        var result = await new ProtectedColumnCheck(server.DataSource).RunAsync(TestContext.Current.CancellationToken);

        RefusalAssert.NamesTheCheckAndTheAddressWithoutTheSecret("ProtectedColumns", result, server);
    }

    [Fact]
    public void ColumnsAsTheMigrationBuildsThemHoldAndAreCounted()
    {
        var finding = ProtectedColumnCheck.Judge(Described());

        Assert.Equal((CheckStatus.Passed, "13 coluna(s) de conteúdo, todas protegidas com a marca da finalidade ou listadas em claro"), (finding.Status, finding.Message));
    }

    [Fact]
    public void UnmarkedColumnOutsideTheListsIsNamedWithWhatToDo()
    {
        var finding = ProtectedColumnCheck.Judge([.. Described(), "item_text|note|text|"]);

        Assert.Equal(
            (CheckStatus.Failed,
                "a coluna item_text.note do esquema content não é protegida (sem a marca de finalidade) nem consta entre as colunas em claro: "
                + "se guarda texto de material, marque-a com COMMENT ON COLUMN e inclua-a em ContentColumns.Protected, como bytea; se não guarda, inclua-a em ContentColumns.Clear"),
            (finding.Status, finding.Message));
    }

    [Fact]
    public void MarkedColumnOutsideTheListIsNamed()
    {
        var finding = ProtectedColumnCheck.Judge([.. Described(), "item_text|note|bytea|protected:texto-bruto"]);

        Assert.Equal(
            (CheckStatus.Failed, "a coluna item_text.note tem a marca de protegida e não consta em ContentColumns.Protected: inclua-a na lista, para o caminho de cifra conhecê-la"),
            (finding.Status, finding.Message));
    }

    [Fact]
    public void ProtectedColumnThatIsNotBytesIsNamedWithItsType()
    {
        var finding = ProtectedColumnCheck.Judge(Replacing("item_text|raw_text|", "item_text|raw_text|text|protected:texto-bruto"));

        Assert.Equal(
            (CheckStatus.Failed, "a coluna protegida item_text.raw_text é do tipo text e não bytea: o cifrado se guarda como bytes"),
            (finding.Status, finding.Message));
    }

    [Fact]
    public void ProtectedColumnWhoseMarkSaysAnotherPurposeIsNamedWithBothMarks()
    {
        var finding = ProtectedColumnCheck.Judge(Replacing("reversal_map|entries|", "reversal_map|entries|bytea|protected:texto-bruto"));

        Assert.Equal(
            (CheckStatus.Failed, "a coluna protegida reversal_map.entries tem a marca 'protected:texto-bruto' e a lista diz 'protected:mapa-reversao': a finalidade da chave tem de ser a mesma"),
            (finding.Status, finding.Message));
    }

    [Fact]
    public void ClearColumnOfAnotherTypeThanItsKindIsNamedWithBothTypes()
    {
        var finding = ProtectedColumnCheck.Judge(Replacing("item_text|recorded_at|", "item_text|recorded_at|text|"));

        Assert.Equal(
            (CheckStatus.Failed, "a coluna item_text.recorded_at está listada em claro como Moment, do tipo timestamp with time zone, e é do tipo text"),
            (finding.Status, finding.Message));
    }

    [Fact]
    public void ListedColumnThatTheSchemaLacksIsNamed()
    {
        var finding = ProtectedColumnCheck.Judge(Described().Where(column => !column.StartsWith("reversal_map|entries|", StringComparison.Ordinal)));

        Assert.Equal(
            (CheckStatus.Failed, "a coluna reversal_map.entries das listas de conteúdo não existe no esquema content: aplique as migrações ou corrija ContentColumns"),
            (finding.Status, finding.Message));
    }

    [DatabaseFact]
    public async Task MigratedDatabasePasses()
    {
        await using var database = await MigratedAsync();

        var result = await AppStartup.CheckAsync(database, dataSource => new ProtectedColumnCheck(dataSource));

        Assert.Equal(CheckStatus.Passed, result.Status);
    }

    [DatabaseFact]
    public async Task TextColumnAddedWithoutMarkOrListIsNamed()
    {
        await using var database = await MigratedAsync();
        await database.RunAsOwnerAsync("ALTER TABLE content.item_text ADD COLUMN zqx_note text");

        var result = await AppStartup.CheckAsync(database, dataSource => new ProtectedColumnCheck(dataSource));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("item_text.zqx_note", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task SealedColumnWhoseMarkWasTakenOffIsNamed()
    {
        await using var database = await MigratedAsync();
        await database.RunAsOwnerAsync("COMMENT ON COLUMN content.item_text.sanitized_text IS NULL");

        var result = await AppStartup.CheckAsync(database, dataSource => new ProtectedColumnCheck(dataSource));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("item_text.sanitized_text", result.Message, StringComparison.Ordinal);
    }

    private static IEnumerable<string> Described() =>
        ContentColumns.Clear.Select(column => $"{column.Table}|{column.Name}|{ClearTypes.Of(column.Kind)}|")
            .Concat(ContentColumns.Protected.Select(column => $"{column.Table}|{column.Name}|bytea|{column.Mark}"));

    private static IEnumerable<string> Replacing(string prefix, string replacement) =>
        Described().Select(column => column.StartsWith(prefix, StringComparison.Ordinal) ? replacement : column);

    private static async Task<ScratchDatabase> MigratedAsync()
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        return database;
    }
}
