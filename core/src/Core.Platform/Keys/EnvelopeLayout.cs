using System.Buffers.Binary;
using System.Text;

namespace Core.Platform.Keys;

/// <summary>Where each part of an envelope sits: the version byte, the label's length and the label in clear (the header), the nonce, the content and the tag.</summary>
internal readonly ref struct EnvelopeLayout
{
    internal const byte Version = 1;
    internal const int NonceLength = 12;
    internal const int TagLength = 16;
    private const int LengthSize = 2;
    private const int FixedHeader = 1 + LengthSize;

    private readonly ReadOnlySpan<byte> envelope;

    private EnvelopeLayout(ReadOnlySpan<byte> envelope, int labelLength)
    {
        this.envelope = envelope;
        LabelLength = labelLength;
    }

    internal int LabelLength { get; }

    internal static int SizeFor(int labelLength, int contentLength) => FixedHeader + labelLength + NonceLength + contentLength + TagLength;

    internal ReadOnlySpan<byte> Header => envelope[..(FixedHeader + LabelLength)];

    internal string Label => Encoding.UTF8.GetString(envelope.Slice(FixedHeader, LabelLength));

    internal ReadOnlySpan<byte> Nonce => envelope.Slice(FixedHeader + LabelLength, NonceLength);

    internal ReadOnlySpan<byte> Content => envelope[(FixedHeader + LabelLength + NonceLength)..^TagLength];

    internal ReadOnlySpan<byte> Tag => envelope[^TagLength..];

    /// <summary>The layout of the bytes, or why they are not an envelope of this format.</summary>
    internal static bool TryRead(ReadOnlySpan<byte> envelope, out EnvelopeLayout layout, out string reason)
    {
        layout = default;
        reason = string.Empty;
        if (envelope.Length < SizeFor(0, 0))
        {
            reason = "The envelope is shorter than the smallest one.";
            return false;
        }

        if (envelope[0] != Version)
        {
            reason = $"The envelope has format version {envelope[0]}, and only {Version} is known.";
            return false;
        }

        var labelLength = BinaryPrimitives.ReadUInt16BigEndian(envelope.Slice(1, LengthSize));
        if (envelope.Length < SizeFor(labelLength, 0))
        {
            reason = "The envelope ends before its label, nonce and tag do.";
            return false;
        }

        layout = new EnvelopeLayout(envelope, labelLength);
        return true;
    }

    /// <summary>Writes the header of an envelope of this label at the start of the buffer.</summary>
    internal static void WriteHeader(Span<byte> buffer, ReadOnlySpan<byte> label)
    {
        buffer[0] = Version;
        BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(1, LengthSize), (ushort)label.Length);
        label.CopyTo(buffer[FixedHeader..]);
    }
}
