using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// The migration 0024 (issue 67, part 2): the server takes the step recovery_tested about the master key, written by the system for an administrative act, with the top of the copy in the id and no
/// reference (or, since the 0033, with the digest of its receipt, taken once); it keeps the clauses of the 0023 and refuses every event of the new step with another shape.
/// </summary>
[Trait("Category", "Database")]
public sealed class CustodyRecoveryTestedListTests : IClassFixture<CustodyDatabase>
{
    private const string Insert = "INSERT INTO public.custody_event (actor_person_id, actor_system, item_kind, item_id, step, reason, corrects, reference) VALUES (";

    private readonly CustodyDatabase database;

    public CustodyRecoveryTestedListTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task Ca5TheStepIsTakenFromTheOwnerWithTheShapeOfTheRecovery()
    {
        await database.RunAsOwnerAsync(Row("NULL, true, 'master_key', 18001, 'recovery_tested', 'administrative_act', NULL, NULL"));

        Assert.Equal(1L, await database.CountAsOwnerAsync("SELECT count(*) FROM public.custody_event WHERE step = 'recovery_tested' AND item_id = 18001"));
    }

    [DatabaseTheory]
    [InlineData("NULL, true, 'person', 18003, 'recovery_tested', 'administrative_act', NULL, NULL")]
    [InlineData("NULL, true, 'master_key', 18004, 'recovery_tested', 'content_read', NULL, NULL")]
    [InlineData("NULL, true, 'master_key', 0, 'recovery_tested', 'administrative_act', NULL, NULL")]
    [InlineData("NULL, true, 'master_key', 18006, 'recovery_tested', 'administrative_act', 1, NULL")]
    [InlineData("NULL, true, 'master_key', 18007, 'recovery_tested', 'administrative_act', NULL, 'master'")]
    [InlineData("NULL, true, 'master_key', 18010, 'key_loss_declared', 'administrative_act', NULL, NULL")]
    [InlineData("NULL, true, 'master_key', 8, 'key_loss_declared', 'administrative_act', NULL, 'master'")]
    [InlineData("NULL, true, 'master_key', 18011, 'key_loss_declared', 'administrative_act', NULL, 'segredo/1/v1'")]
    [InlineData("NULL, true, 'domain', 18012, 'key_loss_declared', 'administrative_act', NULL, 'master'")]
    public async Task Ca5TheClausesOfTheStepsRefuseWhatIsNotTheirShape(string values)
    {
        var refused = await database.RefusedToOwnerAsync(Row(values));

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
    }

    [DatabaseFact]
    public async Task Ca5ARecoveryWrittenByAPersonIsRefused()
    {
        var person = await database.PersonAsync(active: true);

        var refused = await database.RefusedToOwnerAsync(Row($"{person}, false, 'master_key', 18009, 'recovery_tested', 'administrative_act', NULL, NULL"));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "custody_event_key_loss_shape"), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task Ca5TheLossOfAKeyIsStillTakenWithTheShapeOfTheLoss()
    {
        await database.RunAsOwnerAsync(Row("NULL, true, 'master_key', 3, 'key_loss_declared', 'administrative_act', NULL, 'texto-bruto/1/v1'"));

        Assert.Equal(1L, await database.CountAsOwnerAsync("SELECT count(*) FROM public.custody_event WHERE step = 'key_loss_declared' AND reference = 'texto-bruto/1/v1'"));
    }
    [DatabaseFact]
    public async Task Ca584TheDigestOfAReceiptIsTakenOnceAndTheServerRefusesTheSecondEventWithIt()
    {
        var digest = new string('a', 64);
        await database.RunAsOwnerAsync(Row($"NULL, true, 'master_key', 18101, 'recovery_tested', 'administrative_act', NULL, '{digest}'"));

        var refused = await database.RefusedToOwnerAsync(Row($"NULL, true, 'master_key', 18102, 'recovery_tested', 'administrative_act', NULL, '{digest}'"));

        Assert.Equal((PostgresErrorCodes.UniqueViolation, "custody_event_recovery_receipt_once"), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task Ca584EventsOfTheStepWithoutAReferenceAreStillTakenMoreThanOnce()
    {
        await database.RunAsOwnerAsync(Row("NULL, true, 'master_key', 18103, 'recovery_tested', 'administrative_act', NULL, NULL"));
        await database.RunAsOwnerAsync(Row("NULL, true, 'master_key', 18103, 'recovery_tested', 'administrative_act', NULL, NULL"));

        Assert.Equal(2L, await database.CountAsOwnerAsync("SELECT count(*) FROM public.custody_event WHERE step = 'recovery_tested' AND item_id = 18103"));
    }

    [DatabaseTheory]
    [InlineData("abc")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcde")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0")]
    public async Task Ca584TheReferenceOfTheStepIsTheDigestOfTheReceiptAndNothingElse(string reference)
    {
        var refused = await database.RefusedToOwnerAsync(Row($"NULL, true, 'master_key', 18104, 'recovery_tested', 'administrative_act', NULL, '{reference}'"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
    }


    private static string Row(string values) => Insert + values + ")";
}
