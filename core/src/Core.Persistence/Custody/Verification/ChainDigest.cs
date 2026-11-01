using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Core.Persistence.Custody;

/// <summary>
/// The digest of an event, computed here and not by the database: the verification must not ask the function that wrote the digest whether the digest is right.
/// The encoding is the one the chain declares, field by field in a fixed order: the digest of the event before, the sequence, the moment in microseconds since
/// 2000-01-01 UTC, the author (a marker and the person, when there is one), whether the author is the system, the kind, the item, the step and the reason
/// (each text with its length in four bytes in front), the event corrected (a marker and the sequence, when there is one) and who recorded. Numbers are big-endian.
/// </summary>
internal static class ChainDigest
{
    /// <summary>The length of a digest, which is also the length of what the first event points to.</summary>
    internal const int Length = 32;

    private const byte Absent = 0;
    private const byte Present = 1;

    /// <summary>What the first event of the chain points to: as many zero bytes as a digest has.</summary>
    internal static byte[] Origin() => new byte[Length];

    /// <summary>The digest of the event, given the digest of the one before it.</summary>
    internal static byte[] Of(StoredEvent stored, ReadOnlySpan<byte> previous)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(previous);
        AppendNumber(hash, stored.Sequence);
        AppendNumber(hash, stored.OccurredAtMicroseconds);
        AppendOptional(hash, stored.ActorPersonId);
        hash.AppendData([stored.ActorSystem ? Present : Absent]);
        AppendText(hash, stored.ItemKind);
        AppendNumber(hash, stored.ItemId);
        AppendText(hash, stored.Step);
        AppendText(hash, stored.Reason);
        AppendOptional(hash, stored.Corrects);
        AppendText(hash, stored.RecordedBy);
        return hash.GetHashAndReset();
    }

    private static void AppendNumber(IncrementalHash hash, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void AppendOptional(IncrementalHash hash, long? value)
    {
        if (value is null)
        {
            hash.AppendData([Absent]);
            return;
        }

        hash.AppendData([Present]);
        AppendNumber(hash, value.Value);
    }

    private static void AppendText(IncrementalHash hash, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        Span<byte> size = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(size, bytes.Length);
        hash.AppendData(size);
        hash.AppendData(bytes);
    }
}
