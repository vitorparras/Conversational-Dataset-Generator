using Core.Host.Submissions;
using Core.Ingestion;
using Core.Persistence.Tests.Fixtures;

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

        var outcome = await world.SubmitAsync(SubmissionWorld.Request(Messages(("ZQXUM", Forwarded), ("ZQXDOIS", Forwarded))));

        Assert.Equal((SubmissionState.Accepted, 2L, 2L, 2L), (outcome.State, await world.ItemRowsAsync(), await world.OccurrenceRowsAsync(), await world.MarkRowsAsync()));
    }

    [DatabaseFact]
    public async Task TheSenderThatRepeatsItsOwnLongTextIsNotMarked()
    {
        await using var world = await SubmissionWorld.CreateAsync(replication: Rule);

        var outcome = await world.SubmitAsync(SubmissionWorld.Request(Messages(("ZQXUM", Forwarded), ("ZQXUM", Forwarded))));

        Assert.Equal((SubmissionState.Accepted, 2L, 0L), (outcome.State, await world.OccurrenceRowsAsync(), await world.MarkRowsAsync()));
    }

    [DatabaseFact]
    public async Task AShortTextRepeatedByTwoSendersIsNotMarked()
    {
        await using var world = await SubmissionWorld.CreateAsync(replication: Rule);

        var outcome = await world.SubmitAsync(SubmissionWorld.Request(Messages(("ZQXUM", "ZQXok"), ("ZQXDOIS", "ZQXok"))));

        Assert.Equal((SubmissionState.Accepted, 0L), (outcome.State, await world.MarkRowsAsync()));
    }

    [DatabaseFact]
    public async Task TheTextForwardedInASecondSubmissionIsMarkedAgainstTheFirstOccurrence()
    {
        await using var world = await SubmissionWorld.CreateAsync(replication: Rule);

        _ = await world.SubmitAsync(SubmissionWorld.Request(Messages(("ZQXUM", Forwarded), ("ZQXTRES", "ZQXoutra coisa totalmente"))));
        _ = await world.SubmitAsync(SubmissionWorld.Request(Messages(("ZQXDOIS", Forwarded), ("ZQXQUATRO", "ZQXmais uma coisa distinta"))));

        Assert.Equal((4L, 2L), (await world.OccurrenceRowsAsync(), await world.MarkRowsAsync()));
    }

    [DatabaseFact]
    public async Task WithoutTheCriterionInTheServiceNothingIsKeptOfTheReplication()
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var outcome = await world.SubmitAsync(SubmissionWorld.Request(Messages(("ZQXUM", Forwarded), ("ZQXDOIS", Forwarded))));

        Assert.Equal((SubmissionState.Accepted, 0L, 0L), (outcome.State, await world.OccurrenceRowsAsync(), await world.MarkRowsAsync()));
    }

    private static string Messages(params (string Sender, string Text)[] messages) =>
        /*lang=json*/ """{"formato":"cdg-conversa/1","mensagens":[""" +
        string.Join(",", messages.Select((message, at) => $$"""{"id":"m{{at}}","remetente":"{{message.Sender}}","instante":"2031-03-04T08:0{{at}}:00Z","texto":"{{message.Text}}"}""")) + "]}";
}
