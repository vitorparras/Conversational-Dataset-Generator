using Core.Persistence.Custody.Verification;

namespace Core.Persistence.Tests.Verification;

/// <summary>
/// The witness of the seal (#62, criterion 8): the event at the sequence sealed is compared by the digest the verification computes from its content, and not by the column that holds one,
/// so that an event altered with its column left as it was is never the one the seal registers.
/// </summary>
public sealed class SealWitnessTests
{
    private static readonly DateTimeOffset Moment = new(2031, 3, 4, 5, 6, 7, TimeSpan.Zero);

    [Fact]
    public void EventAtTheSequenceSealedWithTheDigestSealedHoldsTheSeal()
    {
        var chain = Chain(5);

        var comparison = Compared(chain, chain[2]);

        Assert.Equal(SealOutcome.Holds, comparison.Outcome);
    }

    [Fact]
    public void EventAlteredKeepingItsColumnOfDigestIsNotTheSealedOneAndTheLinksSayTheSame()
    {
        var chain = Chain(5);
        var sealing = chain[2];
        chain[2] = sealing with { ItemId = sealing.ItemId + 1 };
        var judge = new ChainJudge { Witness = new SealWitness { Sealed = SealOf(sealing) } };

        foreach (var stored in chain)
        {
            judge.Accept(stored);
        }

        Assert.Equal((SealOutcome.DigestDiffers, new InconsistentEvent(3, ChainInconsistency.DigestMismatch)), (judge.Witness.Compare().Outcome, judge.First));
    }

    [Fact]
    public void EventAfterAnAlteredOneIsComparedByWhatItsContentDeservesGivenTheEventBefore()
    {
        var chain = Chain(5);
        var sealing = chain[3];
        chain[1] = chain[1] with { ItemId = 1 };

        var comparison = Compared(chain, sealing);

        Assert.Equal(SealOutcome.Holds, comparison.Outcome);
    }

    [Fact]
    public void TheJudgeThatAlreadyFoundTheFirstInconsistentEventStillGivesTheWitnessTheDigestBeforeEachOne()
    {
        var chain = Chain(5);
        var sealing = chain[3];
        chain[1] = chain[1] with { ItemId = 1 };
        var judge = new ChainJudge { Witness = new SealWitness { Sealed = SealOf(sealing) } };

        foreach (var stored in chain)
        {
            judge.Accept(stored);
        }

        Assert.Equal((SealOutcome.Holds, new InconsistentEvent(2, ChainInconsistency.DigestMismatch)), (judge.Witness.Compare().Outcome, judge.First));
    }

    [Fact]
    public void ChainThatDoesNotReachTheSequenceSealedIsBelowIt()
    {
        var chain = Chain(5);

        var comparison = Compared(chain[..2], chain[2]);

        Assert.Equal(SealOutcome.CountBelowSealed, comparison.Outcome);
    }

    [Fact]
    public void SealOfAChainWithNoEventsHoldsOnEveryChainAndSaysTheOrigin()
    {
        var empty = ChainSeal.Of(Moment, 0, ChainDigest.Origin());

        var witness = new SealWitness { Sealed = empty };
        foreach (var stored in Chain(2))
        {
            witness.Accept(stored, ChainDigest.Origin());
        }

        Assert.Equal(SealOutcome.Holds, witness.Compare().Outcome);
    }

    private static SealComparison Compared(List<StoredEvent> chain, StoredEvent sealing)
    {
        var witness = new SealWitness { Sealed = SealOf(sealing) };
        var previous = ChainDigest.Origin();
        foreach (var stored in chain)
        {
            witness.Accept(stored, previous);
            previous = stored.Digest;
        }

        return witness.Compare();
    }

    private static ChainSeal SealOf(StoredEvent sealing) => ChainSeal.Of(Moment, sealing.Sequence, sealing.Digest);

    private static List<StoredEvent> Chain(int length)
    {
        var chain = new List<StoredEvent>();
        var previous = ChainDigest.Origin();
        for (var sequence = 1; sequence <= length; sequence++)
        {
            var unsigned = new StoredEvent(sequence, 800_000_000_000 + sequence, null, true, "person", 100 + sequence, "person_created", "administrative_act", null, previous, [], "app");
            var stored = unsigned with { Digest = ChainDigest.Of(unsigned, previous) };
            chain.Add(stored);
            previous = stored.Digest;
        }

        return chain;
    }
}
