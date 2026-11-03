using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// The server keeps the shape of the conference of the guarantees whoever writes the event (#68): about the chain, by the system, the item is the mask of what is missing between 0 and 63,
/// the verdict passed is the mask zero and nothing else, and no event corrects another through it.
/// </summary>
[Trait("Category", "Database")]
public sealed class GuaranteesEventShapeTests : IClassFixture<CustodyDatabase>
{
    private const string Shape = "custody_event_verification_shape";

    private readonly CustodyDatabase database;

    public GuaranteesEventShapeTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseTheory]
    [InlineData("custody_chain", "true", 4, "verification_passed")]
    [InlineData("custody_chain", "true", 0, "verification_failed")]
    [InlineData("custody_chain", "true", 64, "verification_failed")]
    [InlineData("custody_chain", "true", -1, "verification_failed")]
    [InlineData("item_text", "true", 0, "verification_passed")]
    [InlineData("custody_chain", "true", 0, "content_read")]
    public async Task EventThatBreaksTheShapeOfAConferenceIsRefusedToAnyRole(string kind, string system, int mask, string reason)
    {
        var refused = await database.RefusedToOwnerAsync(
            "INSERT INTO public.custody_event (actor_person_id, actor_system, item_kind, item_id, step, reason) "
            + $"VALUES (NULL, {system}, '{kind}', {mask}, 'verify_guarantees', '{reason}')");

        Assert.Equal((PostgresErrorCodes.CheckViolation, Shape), (refused.SqlState, refused.ConstraintName));
    }

    [Fact]
    public void EventOfAConferenceIsTheSystemsAboutTheChainWithTheMaskAsItsItem()
    {
        var failed = CustodyEvent.GuaranteesVerified(10);
        var passed = CustodyEvent.GuaranteesVerified(0);

        Assert.Equal((CustodyAuthor.System, CustodyItemKind.CustodyChain, 10L), (failed.Author, failed.Item.Kind, failed.Item.Id));
        Assert.Equal((CustodyReason.VerificationFailed, CustodyStep.VerifyGuarantees), (failed.Reason, failed.Step));
        Assert.Equal(CustodyReason.VerificationPassed, passed.Reason);
        Assert.Null(passed.Corrects);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.GuaranteesVerified(-1));
        var chain = new CustodyItem(CustodyItemKind.CustodyChain, 1);
        _ = Assert.Throws<ArgumentException>(() => CustodyEvent.Of(CustodyAuthor.System, chain, CustodyStep.VerifyGuarantees, CustodyReason.VerificationPassed));
    }
}
