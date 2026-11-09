using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Verification;

/// <summary>
/// The verification of the start meets another writer at the same link of the chain (#742): the chain refuses the verdict with a serialization failure and nothing is written. The verification
/// adds its verdict again, a few times and after a short pause, as the conference of the guarantees does (#677), and does not leave the chain without the verdict until the next interval,
/// which is minutes away.
/// </summary>
[Trait("Category", "Database")]
public sealed class CustodyVerifierConflictTests
{
    private const string Tries = "public.zqx_verdict_tries";
    private const string Verdicts = "SELECT count(*) FROM public.custody_event WHERE step = 'verify_chain'";
    private const string TriesSoFar = $"SELECT CASE WHEN is_called THEN last_value ELSE 0 END FROM {Tries}";

    [DatabaseFact]
    public async Task VerdictThatMeetsAnotherWriterOnceIsAddedAgainAndRecordedOnce()
    {
        await using var database = new CustodyDatabase();
        await ConflictOnVerdictAsync(database, conflicts: 1);

        var verification = await (await VerifierAsync(database)).VerifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.Appended, verification.Recorded.Outcome);
        Assert.Equal((1L, 2L), (await database.CountAsOwnerAsync(Verdicts), await database.CountAsOwnerAsync(TriesSoFar)));
    }

    [DatabaseFact]
    public async Task VerdictThatLosesFourTimesIsRecordedOnTheFifthTry()
    {
        await using var database = new CustodyDatabase();
        await ConflictOnVerdictAsync(database, conflicts: 4);

        var verification = await (await VerifierAsync(database)).VerifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.Appended, verification.Recorded.Outcome);
        Assert.Equal((1L, 5L), (await database.CountAsOwnerAsync(Verdicts), await database.CountAsOwnerAsync(TriesSoFar)));
    }

    [DatabaseFact]
    public async Task VerdictThatAlwaysMeetsAnotherWriterIsTriedFiveTimesAndTheAnswerSaysConflictWithNothingWritten()
    {
        await using var database = new CustodyDatabase();
        await ConflictOnVerdictAsync(database, conflicts: int.MaxValue);

        var verification = await (await VerifierAsync(database)).VerifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.Conflict, verification.Recorded.Outcome);
        Assert.Equal((0L, 5L), (await database.CountAsOwnerAsync(Verdicts), await database.CountAsOwnerAsync(TriesSoFar)));
    }

    /// <summary>The first inserts of the verdict into the chain are refused with a serialization failure, as a writer that meets another would be, and the tries are counted.</summary>
    private static async Task ConflictOnVerdictAsync(CustodyDatabase database, int conflicts)
    {
        await database.RunAsSuperuserAsync(
            $"CREATE SEQUENCE {Tries}; GRANT USAGE ON SEQUENCE {Tries} TO app; GRANT SELECT ON SEQUENCE {Tries} TO owner; "
            + $"CREATE FUNCTION public.zqx_conflict() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF nextval('{Tries}') <= {conflicts} "
            + "THEN RAISE EXCEPTION 'zqx' USING ERRCODE = '40001'; END IF; RETURN NEW; END $$; "
            + "CREATE TRIGGER aaa_zqx_conflict BEFORE INSERT ON public.custody_event FOR EACH ROW WHEN (NEW.step = 'verify_chain') EXECUTE FUNCTION public.zqx_conflict()");
    }

    private static async Task<CustodyVerifier> VerifierAsync(CustodyDatabase database) => new(await database.DataSourceAsync(), TimeProvider.System);
}
