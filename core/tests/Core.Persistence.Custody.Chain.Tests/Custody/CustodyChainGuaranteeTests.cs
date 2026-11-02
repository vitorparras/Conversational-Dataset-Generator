using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>What the server holds of the chain whatever the code does (#58, CA-3): nobody changes it, the server alone fills the links, and the author is never left blank.</summary>
[Trait("Category", "Database")]
public sealed class CustodyChainGuaranteeTests : IClassFixture<CustodyDatabase>
{
    private const string Columns = "(actor_system, item_kind, item_id, step, reason";
    private const string Values = "true, 'person', 5001, 'person_created', 'administrative_act'";
    private const string AnyDigest = "decode(repeat('ab', 32), 'hex')";

    private readonly CustodyDatabase database;

    public CustodyChainGuaranteeTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task ApplicationRoleCannotUpdateAnEvent()
    {
        await AppendOneAsync();

        var refused = await database.RefusedToAppAsync("UPDATE public.custody_event SET step = 'person_deactivated'");

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    [DatabaseFact]
    public async Task OwnerRoleCannotUpdateAnEvent()
    {
        await AppendOneAsync();

        var refused = await database.RefusedToOwnerAsync("UPDATE public.custody_event SET step = 'person_deactivated'");

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    [DatabaseFact]
    public async Task ApplicationRoleCannotDeleteAnEvent()
    {
        await AppendOneAsync();

        var refused = await database.RefusedToAppAsync("DELETE FROM public.custody_event");

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    [DatabaseFact]
    public async Task OwnerRoleCannotDeleteAnEvent()
    {
        await AppendOneAsync();

        var refused = await database.RefusedToOwnerAsync("DELETE FROM public.custody_event");

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    [DatabaseFact]
    public async Task ApplicationRoleCannotEmptyTheChain()
    {
        await AppendOneAsync();

        var refused = await database.RefusedToAppAsync("TRUNCATE public.custody_event");

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    [DatabaseFact]
    public async Task OwnerRoleCannotEmptyTheChain()
    {
        await AppendOneAsync();

        var refused = await database.RefusedToOwnerAsync("TRUNCATE public.custody_event");

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    [DatabaseFact]
    public async Task ApplicationRoleCannotSupplyThePreviousDigest()
    {
        var refused = await database.RefusedToAppAsync($"INSERT INTO public.custody_event {Columns}, previous_digest) VALUES ({Values}, {AnyDigest})");

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    [DatabaseFact]
    public async Task ApplicationRoleCannotSupplyTheDigest()
    {
        var refused = await database.RefusedToAppAsync($"INSERT INTO public.custody_event {Columns}, digest) VALUES ({Values}, {AnyDigest})");

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    [DatabaseFact]
    public async Task ApplicationRoleCannotSupplyTheSequence()
    {
        var refused = await database.RefusedToAppAsync($"INSERT INTO public.custody_event {Columns}, sequence) VALUES ({Values}, 1)");

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    [DatabaseFact]
    public async Task ApplicationRoleCannotSupplyTheMoment()
    {
        var refused = await database.RefusedToAppAsync($"INSERT INTO public.custody_event {Columns}, occurred_at) VALUES ({Values}, now())");

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    [DatabaseFact]
    public async Task PreviousDigestSuppliedByTheOwnerIsReplacedByTheTipOfTheChain()
    {
        await AppendOneAsync();
        var tip = (await database.EventsAsync())[^1].Digest;

        await database.RunAsOwnerAsync($"INSERT INTO public.custody_event {Columns}, previous_digest) VALUES ({Values}, {AnyDigest})");

        var rows = await database.EventsAsync();
        Assert.Equal(tip, rows[^1].PreviousDigest);
        Assert.Equal(CanonicalDigest.Of(rows[^1]), rows[^1].Digest);
    }

    [DatabaseFact]
    public async Task EventWithNoAuthorIsRefused()
    {
        var refused = await database.RefusedToAppAsync(Insert("false", 5007, "person_created", "administrative_act"));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "custody_event_author"), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task EventSignedByAPersonAndByTheSystemAtOnceIsRefused()
    {
        var person = await database.PersonAsync(active: true);

        var statement = Insert("true", 5008, "person_created", "administrative_act")
            .Replace("(actor_system,", "(actor_person_id, actor_system,", StringComparison.Ordinal)
            .Replace("VALUES (", $"VALUES ({person}, ", StringComparison.Ordinal);
        var refused = await database.RefusedToAppAsync(statement);

        Assert.Equal((PostgresErrorCodes.CheckViolation, "custody_event_author"), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task BlankStepIsRefused()
    {
        var refused = await database.RefusedToAppAsync(Insert("true", 5009, "", "administrative_act"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
    }

    [DatabaseFact]
    public async Task PaddedReasonIsRefused()
    {
        var refused = await database.RefusedToAppAsync(Insert("true", 5010, "person_created", " administrative_act"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
    }

    private static string Insert(string system, long item, string step, string reason) =>
        $"INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason) VALUES ({system}, 'person', {item}, '{step}', '{reason}')";

    private async Task AppendOneAsync()
    {
        var chain = await database.ChainAsync();
        var custodyEvent = CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, 5001), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
        _ = await chain.AppendAsync(custodyEvent, TestContext.Current.CancellationToken);
    }
}
