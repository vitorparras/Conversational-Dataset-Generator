using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>One row of <c>public.custody_event</c> as the table holds it.</summary>
internal sealed record EventRow(
    long Sequence,
    DateTimeOffset OccurredAt,
    long? ActorPersonId,
    bool ActorSystem,
    string ItemKind,
    long ItemId,
    string Step,
    string Reason,
    long? Corrects,
    byte[] PreviousDigest,
    byte[] Digest,
    string RecordedBy)
{
    internal static async Task<EventRow> ReadAsync(NpgsqlDataReader reader)
    {
        var token = TestContext.Current.CancellationToken;
        return new EventRow(
            reader.GetInt64(0),
            await reader.GetFieldValueAsync<DateTimeOffset>(1, token),
            await reader.GetFieldValueAsync<long?>(2, token),
            reader.GetBoolean(3),
            reader.GetString(4),
            reader.GetInt64(5),
            reader.GetString(6),
            reader.GetString(7),
            await reader.GetFieldValueAsync<long?>(8, token),
            await reader.GetFieldValueAsync<byte[]>(9, token),
            await reader.GetFieldValueAsync<byte[]>(10, token),
            reader.GetString(11));
    }
}
