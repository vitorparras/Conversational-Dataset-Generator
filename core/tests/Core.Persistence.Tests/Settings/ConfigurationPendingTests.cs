using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Settings;
using Core.Persistence.Tests.People;

namespace Core.Persistence.Tests.Settings;

/// <summary>
/// The list of the submissions waiting for a decision (#634, part 2c; decision 0053): each one is read with the name of who submitted it, the ones already decided are not listed and neither
/// are the ones taken before the moment given, which expired.
/// </summary>
[Trait("Category", "Database")]
public sealed class ConfigurationPendingTests : IClassFixture<PeopleDatabase>
{
    private readonly PeopleDatabase database;

    public ConfigurationPendingTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task ASubmissionWaitingForADecisionIsListedWithTheNameOfWhoSubmittedItAsync()
    {
        var changes = new ConfigurationChanges(await database.ApplicationAsync());
        var name = PeopleDatabase.NewName();
        var written = await SubmitAsync(changes, await NewPersonAsync(name));

        var listed = await changes.PendingAsync(DateTimeOffset.UtcNow.AddHours(-1), TestContext.Current.CancellationToken);

        var pending = Assert.Single(listed, item => item.Change.Id == written);
        Assert.Equal(name, pending.AuthorName);
        Assert.Equal(("Session:IdleTimeout", "00:20:00", "operational_adjustment"), (pending.Change.Key, pending.Change.Value, pending.Change.Motive));
        Assert.False(pending.Change.Decided);
    }

    [DatabaseFact]
    public async Task ADecidedSubmissionIsNotListedAsync()
    {
        var changes = new ConfigurationChanges(await database.ApplicationAsync());
        var written = await SubmitAsync(changes, await NewPersonAsync(PeopleDatabase.NewName()));
        var change = (await changes.SubmittedAsync(written, TestContext.Current.CancellationToken))!;
        _ = await changes.DecideAsync(await NewPersonAsync(PeopleDatabase.NewName()), change, approved: true, TestContext.Current.CancellationToken);

        var listed = await changes.PendingAsync(DateTimeOffset.UtcNow.AddHours(-1), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(listed, item => item.Change.Id == written);
    }

    [DatabaseFact]
    public async Task ASubmissionTakenBeforeTheMomentGivenIsNotListedAsync()
    {
        var changes = new ConfigurationChanges(await database.ApplicationAsync());
        var written = await SubmitAsync(changes, await NewPersonAsync(PeopleDatabase.NewName()));

        var listed = await changes.PendingAsync(DateTimeOffset.UtcNow.AddHours(1), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(listed, item => item.Change.Id == written);
    }

    private static async Task<long> SubmitAsync(ConfigurationChanges changes, long author)
    {
        var submission = new ConfigurationSubmission { Key = "Session:IdleTimeout", Value = "00:20:00", Motive = "operational_adjustment", AuthorId = author };
        return (await changes.SubmitAsync(submission, TestContext.Current.CancellationToken)).ChangeId!.Value;
    }

    private async Task<long> NewPersonAsync(string name)
    {
        var registry = await database.RegistryAsync();
        return PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(name, PersonFunctions.Administration), CustodyAuthor.System, TestContext.Current.CancellationToken));
    }
}
