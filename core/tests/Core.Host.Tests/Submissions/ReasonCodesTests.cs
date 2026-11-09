using System.Text.RegularExpressions;
using Core.Host.Submissions;
using Core.Ingestion;
using Core.Ingestion.Entrance;

namespace Core.Host.Tests.Submissions;

/// <summary>The stable code of a reason (issue 75, contract of the route): written from the name, in lowercase and underscores, and always fit to be the motive of a rejected batch.</summary>
public sealed partial class ReasonCodesTests
{
    [Theory]
    [InlineData(SubmissionRefusal.TooLarge, "too_large")]
    [InlineData(SubmissionRefusal.UnknownFormat, "unknown_format")]
    [InlineData(SubmissionRefusal.FormatNotConfirmed, "format_not_confirmed")]
    [InlineData(SubmissionRefusal.Unreadable, "unreadable")]
    public void RefusalOfTheBarrierIsWrittenInSnakeCase(SubmissionRefusal refusal, string expected) =>
        Assert.Equal(expected, ReasonCodes.Of(refusal));

    [Theory]
    [InlineData(RefusalCode.NoUnits, "no_units")]
    [InlineData(RefusalCode.ConversationThread, "conversation_thread")]
    [InlineData(RefusalCode.PassageSplitsPair, "passage_splits_pair")]
    [InlineData(MaterialField.RepliesTo, "replies_to")]
    [InlineData(MaterialField.Text, "text")]
    public void CodeOfTheConferenceAndOfAFieldIsWrittenInSnakeCase(object value, string expected) =>
        Assert.Equal(expected, value is RefusalCode code ? ReasonCodes.Of(code) : ReasonCodes.Of((MaterialField)value));

    [Fact]
    public void EveryCodeOfTheBarrierAndOfTheConferenceCanBeTheMotiveOfARejectedBatch()
    {
        var codes = Enum.GetValues<SubmissionRefusal>().Select(ReasonCodes.Of).Concat(Enum.GetValues<RefusalCode>().Select(ReasonCodes.Of));

        Assert.All(codes, code => Assert.Matches(Motive(), code));
    }

    [Fact]
    public void NoTwoCodesOfTheBarrierAreTheSame()
    {
        var codes = Enum.GetValues<SubmissionRefusal>().Select(ReasonCodes.Of).ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public void TheCodeOfTheFileTooLargeIsTheOneTheRouteAnswers413For() =>
        Assert.Equal(ReasonCodes.TooLarge, ReasonCodes.Of(SubmissionRefusal.TooLarge));

    [GeneratedRegex("^[a-z][a-z0-9_]{0,62}$")]
    private static partial Regex Motive();
}
