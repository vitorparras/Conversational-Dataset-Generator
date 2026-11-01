using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Tests.Custody;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Verification;

/// <summary>
/// The verification does not ask the function that wrote the digests whether they are right (#61, plano): an owner who replaces <c>custody_digest</c> writes links that a check by that same
/// function would find whole, and the verification, which digests in C# with the encoding the chain declares, points at the first event written with the replaced function.
/// </summary>
[Trait("Category", "Database")]
public sealed class CustodyVerifierIndependenceTests
{
    private const string Replaced =
        "CREATE OR REPLACE FUNCTION public.custody_digest(previous bytea, sequence bigint, occurred_at timestamptz, actor_person_id bigint, actor_system boolean, "
        + "item_kind text, item_id bigint, step text, reason text, corrects bigint, recorded_by text) RETURNS bytea LANGUAGE sql IMMUTABLE "
        + "AS $replaced$ SELECT pg_catalog.sha256(previous || pg_catalog.int8send(sequence)) $replaced$";

    private const string CheckedByTheFunctionItself =
        "SELECT pg_catalog.bool_and(e.digest = public.custody_digest(e.previous_digest, e.sequence, e.occurred_at, e.actor_person_id, e.actor_system, e.item_kind, e.item_id, e.step, "
        + "e.reason, e.corrects, e.recorded_by)) FROM public.custody_event e WHERE e.sequence >= 3";

    [DatabaseFact]
    public async Task DigestFunctionReplacedByTheOwnerIsFoundByTheVerificationAndNotByTheFunctionItself()
    {
        await using var database = new CustodyDatabase();
        var chain = await database.ChainAsync();
        await AppendAsync(chain, 1);
        await AppendAsync(chain, 2);
        await database.RunAsOwnerAsync(Replaced);
        await AppendAsync(chain, 3);
        await AppendAsync(chain, 4);

        var checkedByTheFunction = await database.FlagAsOwnerAsync(CheckedByTheFunctionItself);
        var verification = await new CustodyVerifier(await database.DataSourceAsync(), TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken);

        Assert.True(checkedByTheFunction, "The events written with the replaced function must look whole to that same function, or the test proves nothing.");
        Assert.Equal((false, new InconsistentEvent(3, ChainInconsistency.DigestMismatch)), (verification.Passed, verification.FirstInconsistent));
    }

    [DatabaseFact]
    public async Task ChainWrittenBeforeTheReplacementStaysWholeUntilTheFirstEventWrittenAfterIt()
    {
        await using var database = new CustodyDatabase();
        var chain = await database.ChainAsync();
        await AppendAsync(chain, 1);
        await AppendAsync(chain, 2);
        await database.RunAsOwnerAsync(Replaced);

        var verification = await new CustodyVerifier(await database.DataSourceAsync(), TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken);

        Assert.True(verification.Passed);
        Assert.Equal(2, verification.EventsRead);
    }

    private static async Task AppendAsync(CustodyChain chain, long item)
    {
        var custodyEvent = CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, item), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
        Assert.Equal(CustodyOutcome.Appended, (await chain.AppendAsync(custodyEvent, TestContext.Current.CancellationToken)).Outcome);
    }
}
