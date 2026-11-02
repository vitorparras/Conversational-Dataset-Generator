using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Core.Persistence.Tests.Custody;

/// <summary>The digest of an event worked out here, from the row, by the encoding the migration declares: the proof that the function of the server does what it says.</summary>
internal static class CanonicalDigest
{
    private static readonly DateTimeOffset PostgresEpoch = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    internal static byte[] Of(EventRow row)
    {
        var bytes = new List<byte>();
        bytes.AddRange(row.PreviousDigest);
        bytes.AddRange(Int64(row.Sequence));
        bytes.AddRange(Int64((row.OccurredAt.UtcTicks - PostgresEpoch.UtcTicks) / TimeSpan.TicksPerMicrosecond));
        bytes.AddRange(Optional(row.ActorPersonId));
        bytes.Add(row.ActorSystem ? (byte)1 : (byte)0);
        bytes.AddRange(Text(row.ItemKind));
        bytes.AddRange(Int64(row.ItemId));
        bytes.AddRange(Text(row.Step));
        bytes.AddRange(Text(row.Reason));
        bytes.AddRange(Optional(row.Corrects));
        bytes.AddRange(Text(row.RecordedBy));
        return SHA256.HashData([.. bytes]);
    }

    private static byte[] Int64(long value)
    {
        var bytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] Optional(long? value) => value is { } present ? [1, .. Int64(present)] : [0];

    private static byte[] Text(string value)
    {
        var encoded = Encoding.UTF8.GetBytes(value);
        var length = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, encoded.Length);
        return [.. length, .. encoded];
    }
}
