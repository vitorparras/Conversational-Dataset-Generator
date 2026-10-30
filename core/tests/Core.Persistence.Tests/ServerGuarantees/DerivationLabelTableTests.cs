using Core.Persistence.Migrations;
using Core.Persistence.ServerGuarantees;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Core.Platform.Startup;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>The labels in use live in a table that is born empty, is read with the application's role, and refuses a label that is empty, has spaces around it or is repeated.</summary>
[Trait("Category", "Database")]
public sealed class DerivationLabelTableTests
{
    [DatabaseFact]
    public async Task TableBornEmptyPassesWhateverSetIsProvided()
    {
        await using var database = await MigratedAsync();

        var result = await CheckAsync(database, LabelsSource.Providing());

        Assert.Equal(CheckStatus.Passed, result.Status);
        Assert.StartsWith("0 rótulo(s)", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task LabelsUsedAllInTheSetPass()
    {
        await using var database = await MigratedAsync();
        await database.RecordLabelsAsync(["dominio-a", "dominio-b"]);

        var result = await CheckAsync(database, LabelsSource.Providing("dominio-a", "dominio-b", "dominio-c"));

        Assert.Equal(CheckStatus.Passed, result.Status);
        Assert.StartsWith("2 rótulo(s)", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task LabelUsedAndMissingFromTheSetRefusesNamingIt()
    {
        await using var database = await MigratedAsync();
        await database.RecordLabelsAsync(["dominio-a", "dominio-b"]);

        var result = await CheckAsync(database, LabelsSource.Providing("dominio-a"));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Equal("DerivationLabels", result.Name);
        Assert.Contains("dominio-b", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("dominio-a", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task DatabaseBehindTheStepThatCreatesTheTableRefusesNamingTheMigration()
    {
        await using var database = await ScratchDatabase.CreateAsync();

        var result = await CheckAsync(database, LabelsSource.Providing("dominio-a"));

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("SQLSTATE 42P01", result.Message, StringComparison.Ordinal);
        Assert.Contains("modo migrate", result.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task LabelWithSpacesAroundItIsRefusedByTheTable()
    {
        await using var database = await MigratedAsync();

        var padded = await Record.ExceptionAsync(() => database.RecordLabelsAsync([" dominio-a"]));

        Assert.NotNull(padded);
    }

    [DatabaseFact]
    public async Task EmptyLabelIsRefusedByTheTable()
    {
        await using var database = await MigratedAsync();

        var empty = await Record.ExceptionAsync(() => database.RecordLabelsAsync([string.Empty]));

        Assert.NotNull(empty);
    }

    [DatabaseFact]
    public async Task LabelRecordedTwiceIsRefusedByTheTable()
    {
        await using var database = await MigratedAsync();
        await database.RecordLabelsAsync(["dominio-a"]);

        var again = await Record.ExceptionAsync(() => database.RecordLabelsAsync(["dominio-a"]));

        Assert.NotNull(again);
    }

    private static async Task<ScratchDatabase> MigratedAsync()
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        return database;
    }

    private static Task<CheckResult> CheckAsync(ScratchDatabase database, IMasterKeySource source) =>
        AppStartup.CheckAsync(database, dataSource => new DerivationLabelCheck(dataSource, source));
}
