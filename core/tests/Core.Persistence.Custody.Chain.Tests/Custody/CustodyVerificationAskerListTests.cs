using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// The migration 0037 (issue 605, CA-4): the event verify_chain may carry, in the reference, requested-by/id of whoever asked for the verification, and has none when the host ran it by itself;
/// it takes no other reference, and the steps that were not allowed a reference still are not.
/// </summary>
[Trait("Category", "Database")]
public sealed class CustodyVerificationAskerListTests : IClassFixture<CustodyDatabase>
{
    private const string Insert = "INSERT INTO public.custody_event (actor_person_id, actor_system, item_kind, item_id, step, reason, corrects, reference) VALUES (";

    private readonly CustodyDatabase database;

    public CustodyVerificationAskerListTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task AVerificationAskedForIsTakenWithTheAskerInTheReference()
    {
        await database.RunAsOwnerAsync(Row("NULL, true, 'custody_chain', 60501, 'verify_chain', 'verification_passed', NULL, 'requested-by/41'"));

        Assert.Equal(1L, await database.CountAsOwnerAsync("SELECT count(*) FROM public.custody_event WHERE step = 'verify_chain' AND item_id = 60501 AND reference = 'requested-by/41'"));
    }

    [DatabaseFact]
    public async Task AVerificationTheHostRanByItselfIsStillTakenWithNoReference()
    {
        await database.RunAsOwnerAsync(Row("NULL, true, 'custody_chain', 60502, 'verify_chain', 'verification_passed', NULL, NULL"));

        Assert.Equal(1L, await database.CountAsOwnerAsync("SELECT count(*) FROM public.custody_event WHERE step = 'verify_chain' AND item_id = 60502 AND reference IS NULL"));
    }

    [DatabaseTheory]
    [InlineData("requested-by/0")]
    [InlineData("requested-by/041")]
    [InlineData("requested-by/-3")]
    [InlineData("requested-by/41x")]
    [InlineData("requested-by/")]
    [InlineData("requested-by/1234567890123456789")]
    [InlineData("Requested-By/41")]
    [InlineData("seal/7")]
    public async Task TheReferenceOfAVerificationIsTheAskerAndNothingElse(string reference)
    {
        var refused = await database.RefusedToOwnerAsync(Row($"NULL, true, 'custody_chain', 60503, 'verify_chain', 'verification_passed', NULL, '{reference}'"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
    }

    [DatabaseFact]
    public async Task AStepThatWasNotAllowedAReferenceStillIsNot()
    {
        var refused = await database.RefusedToOwnerAsync(Row("NULL, true, 'custody_chain', 60504, 'verify_guarantees', 'verification_passed', NULL, 'requested-by/41'"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
    }

    private static string Row(string values) => Insert + values + ")";
}
