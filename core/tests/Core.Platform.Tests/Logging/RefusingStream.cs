namespace Core.Platform.Tests.Logging;

/// <summary>A file whose disk is full: it opens, then refuses every byte written to it and keeps refusing to flush them.</summary>
internal sealed class RefusingStream : Stream
{
    private int refused;

    public bool Closed { get; private set; }

    public int Writes { get; private set; }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => !Closed;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Writes++;
        refused += count;
        throw new IOException("ZQX-DISK-FULL-7731");
    }

    public override void Flush()
    {
        if (refused > 0)
        {
            throw new IOException("ZQX-DISK-FULL-7731");
        }
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        Closed = true;
        base.Dispose(disposing);
    }
}
