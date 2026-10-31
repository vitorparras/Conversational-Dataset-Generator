using Core.Persistence.Migrations;
using Core.Persistence.ServerGuarantees;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// The labels in use live in a table that is born empty, is read with the application's role, and refuses a label that is empty,
/// has spaces around it or is repeated; a label once recorded is never changed nor dropped, not even by the owner.
/// </summary>
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
    public async Task LabelWithWhiteSpaceAroundItIsRefusedByTheCheck()
    {
        await using var database = await MigratedAsync();

        foreach (var label in new[] { " dominio-a", "dominio-a ", "\tdominio-a", "dominio-a\n" })
        {
            var padded = await Assert.ThrowsAsync<PostgresException>(() => database.RecordLabelsAsync([label]));

            Assert.Equal(PostgresErrorCodes.CheckViolation, padded.SqlState);
        }
    }

    [DatabaseFact]
    public async Task EmptyLabelIsRefusedByTheCheck()
    {
        await using var database = await MigratedAsync();

        var empty = await Assert.ThrowsAsync<PostgresException>(() => database.RecordLabelsAsync([string.Empty]));

        Assert.Equal(PostgresErrorCodes.CheckViolation, empty.SqlState);
    }

    [DatabaseFact]
    public async Task LabelRecordedTwiceIsRefusedByThePrimaryKey()
    {
        await using var database = await MigratedAsync();
        await database.RecordLabelsAsync(["dominio-a"]);

        var again = await Assert.ThrowsAsync<PostgresException>(() => database.RecordLabelsAsync(["dominio-a"]));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, again.SqlState);
    }

    [DatabaseFact]
    public async Task OwnerCannotDeleteALabelAndItStays()
    {
        await using var database = await MigratedAsync();
        await database.RecordLabelsAsync(["dominio-a", "dominio-b"]);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => database.RunAsOwnerAsync("DELETE FROM public.derivation_label WHERE label = 'dominio-a'"));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, refused.SqlState);
        Assert.Equal(["dominio-a", "dominio-b"], await LabelsAsync(database));
    }

    [DatabaseFact]
    public async Task OwnerCannotDeleteEveryLabelAtOnce()
    {
        await using var database = await MigratedAsync();
        await database.RecordLabelsAsync(["dominio-a"]);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => database.RunAsOwnerAsync("DELETE FROM public.derivation_label"));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, refused.SqlState);
        Assert.Equal(["dominio-a"], await LabelsAsync(database));
    }

    [DatabaseFact]
    public async Task OwnerCannotRenameALabelAndItStays()
    {
        await using var database = await MigratedAsync();
        await database.RecordLabelsAsync(["dominio-a"]);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => database.RunAsOwnerAsync("UPDATE public.derivation_label SET label = 'dominio-z'"));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, refused.SqlState);
        Assert.Equal(["dominio-a"], await LabelsAsync(database));
    }

    [DatabaseFact]
    public async Task OwnerCannotTruncateTheTable()
    {
        await using var database = await MigratedAsync();
        await database.RecordLabelsAsync(["dominio-a"]);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => database.RunAsOwnerAsync("TRUNCATE public.derivation_label"));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, refused.SqlState);
        Assert.Equal(["dominio-a"], await LabelsAsync(database));
    }

    [DatabaseFact]
    public async Task RefusalNamesTheOperationAndTheTableButNoLabel()
    {
        await using var database = await MigratedAsync();
        await database.RecordLabelsAsync(["dominio-a"]);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => database.RunAsOwnerAsync("DELETE FROM public.derivation_label"));

        Assert.Contains("DELETE on derivation_label", refused.MessageText, StringComparison.Ordinal);
        Assert.DoesNotContain("dominio-a", refused.MessageText, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task BothTriggersFireAlsoInASessionThatSkipsTheOrdinaryOnes()
    {
        await using var database = await MigratedAsync();

        var modes = await database.TriggerModesAsOwnerAsync();

        Assert.Equal(["A", "A"], modes);
    }

    [DatabaseFact]
    public async Task LabelCanStillBeRecordedAfterTheRefusals()
    {
        await using var database = await MigratedAsync();
        await database.RecordLabelsAsync(["dominio-a"]);
        _ = await Assert.ThrowsAsync<PostgresException>(() => database.RunAsOwnerAsync("DELETE FROM public.derivation_label"));

        await database.RecordLabelsAsync(["dominio-b"]);

        Assert.Equal(["dominio-a", "dominio-b"], await LabelsAsync(database));
    }

    private static Task<IReadOnlyList<string>> LabelsAsync(ScratchDatabase database) =>
        database.RecordedLabelsAsOwnerAsync();

    private static async Task<ScratchDatabase> MigratedAsync()
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        return database;
    }

    private static Task<CheckResult> CheckAsync(ScratchDatabase database, IMasterKeySource source) =>
        AppStartup.CheckAsync(database, dataSource => new DerivationLabelCheck(dataSource, source));
}
