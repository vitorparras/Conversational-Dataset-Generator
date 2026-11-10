using Core.Ingestion;
using Core.Persistence.Tests.Fixtures;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// What the acervo still holds of a message once its content is marked as replicated (issue 82, criterion 4): the occurrence stays, with the digest of its sender,
/// its moment and the conversation it came in, and the item is neither removed nor merged with the other.
/// </summary>
public sealed class ReplicationDumpTests
{
    private const string Forwarded = "ZQXordem repassada em cadeia";

    [DatabaseFact]
    public async Task EveryOccurrenceStaysWithItsSenderItsMomentAndItsConversationWhetherMarkedOrNotAsync()
    {
        await using var world = await SubmissionWorld.CreateAsync(replication: new ReplicationRule(minLength: 10, minSenders: 2));
        var body = SubmissionWorld.Messages(("ZQXUM", Forwarded), ("ZQXDOIS", Forwarded), ("ZQXTRES", "ZQXoutra coisa totalmente"));

        _ = await world.SubmitAsync(SubmissionWorld.Request(body));

        var dumped = await world.StoredAsync(
            "SELECT to_char(o.said_at AT TIME ZONE 'UTC', 'HH24:MI') || ',' || (length(o.sender_digest) > 0) || ',' || (i.batch_id = o.batch_id) "
            + "FROM content.replication_occurrence o JOIN content.item_text i ON i.id = o.item_id ORDER BY o.id");
        var senders = await world.StoredAsync("SELECT count(DISTINCT sender_digest)::text FROM content.replication_occurrence");
        Assert.Equal(("08:00,true,true;08:01,true,true;08:02,true,true", "3", 3L, 2L), (string.Join(';', dumped), senders[0], await world.ItemRowsAsync(), await world.MarkRowsAsync()));
    }
}
