using Core.Persistence.Custody.Verification;

namespace Core.Persistence.Tests.Verification;

/// <summary>The judge of the verification: a chain whose every link holds is told whole, and the first event that does not is told with how, whatever follows it.</summary>
public sealed class ChainJudgeTests
{
    [Fact]
    public void EmptyChainHoldsAndReadsNothing()
    {
        var judge = Judged([]);

        Assert.Equal((0L, null), (judge.Read, judge.First));
    }

    [Fact]
    public void ChainWhoseLinksAllHoldIsTold()
    {
        var judge = Judged(Chain(5));

        Assert.Equal((5L, null), (judge.Read, judge.First));
    }

    [Fact]
    public void EventWhoseContentWasChangedKeepingItsDigestIsThatEvent()
    {
        var chain = Chain(5);
        chain[2] = chain[2] with { ItemId = chain[2].ItemId + 1 };

        var judge = Judged(chain);

        Assert.Equal(new InconsistentEvent(3, ChainInconsistency.DigestMismatch), judge.First);
    }

    [Fact]
    public void EventWhosePreviousDigestWasSwappedIsThatEvent()
    {
        var chain = Chain(5);
        chain[3] = chain[3] with { PreviousDigest = new byte[ChainDigest.Length] };

        var judge = Judged(chain);

        Assert.Equal(new InconsistentEvent(4, ChainInconsistency.PreviousDigestMismatch), judge.First);
    }

    [Fact]
    public void EventRemovedFromTheMiddleIsToldAtTheOneAfterIt()
    {
        var chain = Chain(5);
        chain.RemoveAt(1);

        var judge = Judged(chain);

        Assert.Equal(new InconsistentEvent(3, ChainInconsistency.SequenceGap), judge.First);
    }

    [Fact]
    public void ChainThatDoesNotStartAtOneIsToldAtItsFirstEvent()
    {
        var chain = Chain(3);
        chain.RemoveAt(0);

        var judge = Judged(chain);

        Assert.Equal(new InconsistentEvent(2, ChainInconsistency.SequenceGap), judge.First);
    }

    [Fact]
    public void FirstEventThatDoesNotPointToTheOriginIsToldAsPreviousDigest()
    {
        var chain = Chain(2);
        chain[0] = chain[0] with { PreviousDigest = [.. Enumerable.Repeat((byte)1, ChainDigest.Length)] };

        var judge = Judged(chain);

        Assert.Equal(new InconsistentEvent(1, ChainInconsistency.PreviousDigestMismatch), judge.First);
    }

    [Fact]
    public void FirstInconsistentEventStaysTheFirstAndTheCountGoesOnToTheEnd()
    {
        var chain = Chain(6);
        chain[1] = chain[1] with { Reason = "content_read" };
        chain[4] = chain[4] with { Step = "correction" };

        var judge = Judged(chain);

        Assert.Equal((6L, new InconsistentEvent(2, ChainInconsistency.DigestMismatch)), (judge.Read, judge.First));
    }

    [Fact]
    public void ChangeOfEachFieldTheDigestCoversIsToldAtTheEventChanged()
    {
        var changes = new Func<StoredEvent, StoredEvent>[]
        {
            stored => stored with { OccurredAtMicroseconds = stored.OccurredAtMicroseconds + 1 },
            stored => stored with { ActorPersonId = 7 },
            stored => stored with { ActorSystem = !stored.ActorSystem },
            stored => stored with { ItemKind = "item_text" },
            stored => stored with { ItemId = stored.ItemId + 1 },
            stored => stored with { Step = "person_deactivated" },
            stored => stored with { Reason = "correction_of_record" },
            stored => stored with { Corrects = 1 },
            stored => stored with { RecordedBy = "other" },
        };

        foreach (var change in changes)
        {
            var chain = Chain(3);
            chain[1] = change(chain[1]);

            Assert.Equal(new InconsistentEvent(2, ChainInconsistency.DigestMismatch), Judged(chain).First);
        }
    }

    [Fact]
    public void TextIsDigestedWithItsLengthSoThatTwoDifferentEventsNeverEncodeAlike()
    {
        var one = Event(1, ChainDigest.Origin()) with { Step = "ab", Reason = "c" };
        var other = one with { Step = "a", Reason = "bc" };

        Assert.NotEqual(ChainDigest.Of(one, ChainDigest.Origin()), ChainDigest.Of(other, ChainDigest.Origin()));
    }

    [Fact]
    public void AbsentNumberIsNotTheNumberZero()
    {
        var absent = Event(1, ChainDigest.Origin());
        var zero = absent with { Corrects = 0 };

        Assert.NotEqual(ChainDigest.Of(absent, ChainDigest.Origin()), ChainDigest.Of(zero, ChainDigest.Origin()));
    }

    private static ChainJudge Judged(IEnumerable<StoredEvent> events)
    {
        var judge = new ChainJudge();
        foreach (var stored in events)
        {
            judge.Accept(stored);
        }

        return judge;
    }

    private static List<StoredEvent> Chain(int length)
    {
        var chain = new List<StoredEvent>();
        var previous = ChainDigest.Origin();
        for (var sequence = 1; sequence <= length; sequence++)
        {
            var stored = Event(sequence, previous);
            chain.Add(stored);
            previous = stored.Digest;
        }

        return chain;
    }

    private static StoredEvent Event(long sequence, byte[] previous)
    {
        var unsigned = new StoredEvent(sequence, 800_000_000_000 + sequence, null, true, "person", 100 + sequence, "person_created", "administrative_act", null, previous, [], "app");
        return unsigned with { Digest = ChainDigest.Of(unsigned, previous) };
    }
}
