using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.ServerGuarantees;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.People;

/// <summary>
/// The map of <see cref="AuditMap"/> against the real server (#60, part 2): what the registry and the store of passwords do as the
/// application's role is in the server's log in the number the chain holds events, except the one difference that is known.
/// </summary>
[Trait("Category", "Database")]
public sealed class PeopleAuditFidelityTests : IClassFixture<PeopleDatabase>
{
    // nosemgrep: secret-literal-csharp a synthetic password of this test, which hashes it into a throwaway database.
    private const string Password = "zqx-senha-sintetica";
    private readonly PeopleDatabase database;

    public PeopleAuditFidelityTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task EveryChangeOfAPersonIsSeenByTheServerAsManyTimesAsTheChainRecordsIt()
    {
        var from = DateTimeOffset.UtcNow.AddMinutes(-1);
        var token = TestContext.Current.CancellationToken;
        var registry = await database.RegistryAsync();
        var credentials = await database.CredentialsAsync(workFactor: 1);
        var first = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), PersonFunctions.Accepted[0]), CustodyAuthor.System, token));
        var second = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), PersonFunctions.Accepted[0]), CustodyAuthor.System, token));
        _ = await registry.ChangeFunctionAsync(first, PersonFunctions.Accepted[1], CustodyAuthor.System, token);
        _ = await credentials.SetPasswordAsync(first, Password, CustodyAuthor.System, token);
        _ = await credentials.SetPasswordAsync(first, Password + "2", CustodyAuthor.System, token);
        _ = await registry.DeactivateAsync(second, CustodyAuthor.System, token);

        var differences = await AuditFidelity.DifferencesAsync(await database.ApplicationAsync(), from);

        Assert.DoesNotContain(differences, difference => difference.Role == "app");
    }

    [DatabaseFact]
    public async Task RecomputationAtSignInIsTheKnownDifferenceAndNamesTheIssue()
    {
        var from = DateTimeOffset.UtcNow.AddMinutes(-1);
        var token = TestContext.Current.CancellationToken;
        var registry = await database.RegistryAsync();
        var person = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), PersonFunctions.Accepted[0]), CustodyAuthor.System, token));
        _ = await (await database.CredentialsAsync(workFactor: 1)).SetPasswordAsync(person, Password, CustodyAuthor.System, token);

        _ = await (await database.CredentialsAsync(workFactor: 2)).VerifyAsync(person, Password, token);
        var differences = await AuditFidelity.DifferencesAsync(await database.ApplicationAsync(), from);

        var known = Assert.Single(differences, difference => difference.Role == "app");
        Assert.Equal(("public.person_credential", 1), (known.Table, known.Seen - known.Recorded));
        Assert.Equal(AuditMap.RecomputeWithoutEvent, known.KnownGap);
    }
}
