using System.Globalization;
using Core.Host.Submissions;
using Core.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// What the submission leaves of the replicated content (issue 82): each message of a conversation is kept as digests, the same long text of two senders is marked in both,
/// a repeat of the same sender is not, and nothing is removed.
/// </summary>
public sealed class SubmissionReplicationTests
{
    private const string Forwarded = "ZQXordem repassada em cadeia";

    private static readonly ReplicationRule Rule = new(minLength: 10, minSenders: 2);

    [DatabaseFact]
    public async Task TheSameLongTextOfTwoSendersIsMarkedInBothAndEveryMessageStays()
    {
        await using var world = await SubmissionWorld.CreateAsync(replication: Rule);

        var outcome = await world.SubmitAsync(SubmissionWorld.Request(SubmissionWorld.Messages(("ZQXUM", Forwarded), ("ZQXDOIS", Forwarded))));

        Assert.Equal((SubmissionState.Accepted, 2L, 2L, 2L), (outcome.State, await world.ItemRowsAsync(), await world.OccurrenceRowsAsync(), await world.MarkRowsAsync()));
    }

    [DatabaseFact]
    public async Task TheSenderThatRepeatsItsOwnLongTextIsNotMarked()
    {
        await using var world = await SubmissionWorld.CreateAsync(replication: Rule);

        var outcome = await world.SubmitAsync(SubmissionWorld.Request(SubmissionWorld.Messages(("ZQXUM", Forwarded), ("ZQXUM", Forwarded))));

        Assert.Equal((SubmissionState.Accepted, 2L, 0L), (outcome.State, await world.OccurrenceRowsAsync(), await world.MarkRowsAsync()));
    }

    [DatabaseFact]
    public async Task AShortTextRepeatedByTwoSendersIsNotMarked()
    {
        await using var world = await SubmissionWorld.CreateAsync(replication: Rule);

        var outcome = await world.SubmitAsync(SubmissionWorld.Request(SubmissionWorld.Messages(("ZQXUM", "ZQXok"), ("ZQXDOIS", "ZQXok"))));

        Assert.Equal((SubmissionState.Accepted, 0L), (outcome.State, await world.MarkRowsAsync()));
    }

    [DatabaseFact]
    public async Task TheTextForwardedInASecondSubmissionIsMarkedAgainstTheFirstOccurrence()
    {
        await using var world = await SubmissionWorld.CreateAsync(replication: Rule);

        _ = await world.SubmitAsync(SubmissionWorld.Request(SubmissionWorld.Messages(("ZQXUM", Forwarded), ("ZQXTRES", "ZQXoutra coisa totalmente"))));
        _ = await world.SubmitAsync(SubmissionWorld.Request(SubmissionWorld.Messages(("ZQXDOIS", Forwarded), ("ZQXQUATRO", "ZQXmais uma coisa distinta"))));

        Assert.Equal((4L, 2L), (await world.OccurrenceRowsAsync(), await world.MarkRowsAsync()));
    }

    [DatabaseFact]
    public async Task WithoutTheCriterionInTheServiceNothingIsKeptOfTheReplication()
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var outcome = await world.SubmitAsync(SubmissionWorld.Request(SubmissionWorld.Messages(("ZQXUM", Forwarded), ("ZQXDOIS", Forwarded))));

        Assert.Equal((SubmissionState.Accepted, 0L, 0L), (outcome.State, await world.OccurrenceRowsAsync(), await world.MarkRowsAsync()));
    }

    [DatabaseTheory]
    [InlineData("ZQXa\U0001F642", 0L)]
    [InlineData("ZQXab\U0001F642", 2L)]
    public async Task AnEmojiCountsAsOneCodePointAgainstTheMinimumOfTheDomain(string text, long marks)
    {
        await using var world = await SubmissionWorld.CreateAsync(replication: new ReplicationRule(minLength: 6, minSenders: 2));

        _ = await world.SubmitAsync(SubmissionWorld.Request(SubmissionWorld.Messages(("ZQXUM", text), ("ZQXDOIS", text))));

        Assert.Equal(marks, await world.MarkRowsAsync());
    }

    [DatabaseTheory]
    [InlineData(10, 2, 2L)]
    [InlineData(29, 2, 0L)]
    [InlineData(10, 3, 0L)]
    public async Task TheSameMaterialUnderOtherValuesOfTheConfigurationGivesOtherMarksAndTheOriginOfEachValueIsShown(int length, int senders, long marks)
    {
        var loaded = ClosedConfiguration.Load(
            new ConfigurationSchema([ReplicationConfiguration.Section]),
            new StrictArgumentsSource([$"--Replication:MinLength={length}", $"--Replication:MinSenders={senders}"]));
        await using var world = await SubmissionWorld.CreateAsync(replication: ReplicationConfiguration.Rule(loaded.Values));

        _ = await world.SubmitAsync(SubmissionWorld.Request(SubmissionWorld.Messages(("ZQXUM", Forwarded), ("ZQXDOIS", Forwarded))));

        Assert.Equal(marks, await world.MarkRowsAsync());
        Assert.Contains(new EffectiveEntry("Replication:MinLength", length.ToString(CultureInfo.InvariantCulture), ConfigurationOrigin.Argument), loaded.View.Entries);
        Assert.Contains(new EffectiveEntry("Replication:MinSenders", senders.ToString(CultureInfo.InvariantCulture), ConfigurationOrigin.Argument), loaded.View.Entries);
    }

    [DatabaseFact]
    public async Task TwoBatchesOfTheSameDomainAtTheSameTimeBothComeInAndNeitherSeesTheOther()
    {
        await using var world = await SubmissionWorld.CreateAsync(replication: Rule);
        var first = SubmissionWorld.Messages(("ZQXUM", Forwarded), ("ZQXDOIS", Forwarded));
        var second = SubmissionWorld.Messages(("ZQXTRES", Forwarded), ("ZQXQUATRO", Forwarded));

        var runs = new[] { world.SubmitAsync(SubmissionWorld.Request(first)), world.SubmitAsync(SubmissionWorld.Request(second)) };
        var refused = await Record.ExceptionAsync(() => Task.WhenAll(runs));
        var states = string.Join(',', runs.Select(Described));
        var seen = (states, await world.OccurrenceRowsAsync(), await world.MarkRowsAsync());
        Assert.True(seen == ("Accepted,Accepted", 4L, 4L), "error: " + seen + " " + refused?.ToString().ReplaceLineEndings(" | "));
    }

    private static string Described(Task<SubmissionOutcome> run) =>
        run.IsCompletedSuccessfully ? run.Result.Kind + "/" + run.Result.State + "/" + run.Result.Code + "/" + run.Result.BlockedBy + "/" + run.Result.Failure : "faulted";
}
