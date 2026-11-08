namespace Core.Ingestion.Tests.Entrance;

/// <summary>A stream of the bytes given that counts what it hands out and gives them in pieces no larger than the piece set, so that a test can tell how far the gate reads.</summary>
internal sealed class CountingStream(byte[] content, int piece = int.MaxValue) : Stream
{
    private int position;

    /// <summary>How many bytes the stream has handed out.</summary>
    public long Delivered => position;

    /// <summary>The largest count of bytes any one read asked for.</summary>
    public int LargestRequest { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => content.Length;

    public override long Position
    {
        get => position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        LargestRequest = Math.Max(LargestRequest, buffer.Length);
        var count = Math.Min(Math.Min(buffer.Length, piece), content.Length - position);
        content.AsSpan(position, count).CopyTo(buffer);
        position += count;
        return count;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromResult(Read(buffer.Span));

    public override void Flush() => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
