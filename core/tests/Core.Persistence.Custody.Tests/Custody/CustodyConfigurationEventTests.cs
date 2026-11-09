using Core.Persistence.Custody;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// The events of a change of configuration (issue 634): the submission is the author's, and the decision is another person's, approving or refusing, with the sequence of the submission as the reference;
/// the item is the change in both, and neither the key nor the value travels in the event.
/// </summary>
public sealed class CustodyConfigurationEventTests
{
    [Fact]
    public void TheSubmissionIsThePersonsAboutThatChangeForAnAdministrativeActWithNoReference()
    {
        var submitted = CustodyEvent.ConfigurationSubmitted(7, 41);

        Assert.Equal(
            (CustodyAuthor.Person(7), new CustodyItem(CustodyItemKind.ConfigurationChange, 41), CustodyStep.ConfigurationSubmitted, CustodyReason.AdministrativeAct, null, null),
            (submitted.Author, submitted.Item, submitted.Step, submitted.Reason, submitted.Reference, submitted.Corrects));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TheSubmissionOfAChangeWithoutAPositiveNumberIsRefusedByTheChangeParameter(long changeId)
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.ConfigurationSubmitted(7, changeId));

        Assert.Equal("changeId", refused.ParamName);
    }

    [Theory]
    [InlineData(true, CustodyStep.ConfigurationApproved, "300")]
    [InlineData(false, CustodyStep.ConfigurationRefused, "300")]
    public void TheDecisionIsThePersonsAboutThatChangeWithTheStepOfTheAnswerAndTheSequenceOfTheSubmissionAsTheReference(bool approved, CustodyStep step, string reference)
    {
        var decided = CustodyEvent.ConfigurationDecided(9, 41, 300, approved);

        Assert.Equal(
            (CustodyAuthor.Person(9), new CustodyItem(CustodyItemKind.ConfigurationChange, 41), step, CustodyReason.AdministrativeAct, reference, null),
            (decided.Author, decided.Item, decided.Step, decided.Reason, decided.Reference, decided.Corrects));
    }

    [Theory]
    [InlineData(0, 300, "changeId")]
    [InlineData(-1, 300, "changeId")]
    [InlineData(41, 0, "submissionSequence")]
    [InlineData(41, -1, "submissionSequence")]
    public void TheDecisionOnANumberThatIsNoChangeOrNoSequenceIsRefusedByItsParameter(long changeId, long submissionSequence, string parameter)
    {
        var approving = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.ConfigurationDecided(9, changeId, submissionSequence, approved: true));
        var refusing = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.ConfigurationDecided(9, changeId, submissionSequence, approved: false));

        Assert.Equal((parameter, parameter), (approving.ParamName, refusing.ParamName));
    }

    [Theory]
    [InlineData(CustodyStep.ConfigurationSubmitted)]
    [InlineData(CustodyStep.ConfigurationApproved)]
    [InlineData(CustodyStep.ConfigurationRefused)]
    public void TheThreeStepsAreRefusedAsAPlainEventByTheStepParameter(CustodyStep step)
    {
        var item = new CustodyItem(CustodyItemKind.ConfigurationChange, 41);

        var refused = Assert.Throws<ArgumentException>(() => CustodyEvent.Of(CustodyAuthor.Person(7), item, step, CustodyReason.AdministrativeAct));

        Assert.Equal("step", refused.ParamName);
    }
}
