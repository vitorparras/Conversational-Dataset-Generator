using Core.Persistence.Custody;

namespace Core.Persistence.Tests.Custody;

/// <summary>What the chain answers: the sequence when the event is in, and when it is not, which refusal and a text for whoever operates.</summary>
public sealed class CustodyResultTests
{
    [Fact]
    public void AppendedCarriesTheSequenceAndNoRefusal()
    {
        var result = CustodyResult.Appended(12);

        Assert.Equal((CustodyOutcome.Appended, 12L, null), (result.Outcome, result.Sequence, result.Refusal));
    }

    [Fact]
    public void RefusalCarriesTheReasonAndNoSequence()
    {
        var result = CustodyResult.Refused(CustodyOutcome.Conflict, "texto");

        Assert.Equal((CustodyOutcome.Conflict, null, "texto"), (result.Outcome, result.Sequence, result.Refusal));
    }

    [Fact]
    public void EachRefusalHasItsOwnOutcomeAndAReasonInPortuguese()
    {
        var refusals = new[]
        {
            (CustodyOutcome.ActorUnknown, CustodyRefusals.ActorUnknown()),
            (CustodyOutcome.ActorDeactivated, CustodyRefusals.ActorDeactivated()),
            (CustodyOutcome.CorrectedNotFound, CustodyRefusals.CorrectedNotFound()),
            (CustodyOutcome.Conflict, CustodyRefusals.Conflict()),
        };

        Assert.All(refusals, refusal => Assert.Equal(refusal.Item1, refusal.Item2.Outcome));
        Assert.All(refusals, refusal => Assert.EndsWith(".", refusal.Item2.Refusal, StringComparison.Ordinal));
    }

    [Fact]
    public void ConflictTellsTheRequestMayBeRepeated() =>
        Assert.Contains("repetido", CustodyRefusals.Conflict().Refusal, StringComparison.Ordinal);
}
