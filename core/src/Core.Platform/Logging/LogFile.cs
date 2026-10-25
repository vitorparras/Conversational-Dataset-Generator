namespace Core.Platform.Logging;

/// <summary>The open log file: one writer at a time, each text flushed before the call returns, nothing written once closed.</summary>
internal sealed class LogFile : IDisposable
{
    private readonly Lock gate = new();
    private readonly StreamWriter writer;
    private bool closed;

    internal LogFile(StreamWriter writer)
    {
        this.writer = writer;
    }

    /// <summary>Closes the file; what is appended afterwards is dropped, never thrown.</summary>
    public void Dispose()
    {
        lock (gate)
        {
            closed = true;
            writer.Dispose();
        }
    }

    /// <summary>Writes the text and flushes it, so a process that dies right after loses none of it.</summary>
    internal void Append(string text)
    {
        lock (gate)
        {
            if (closed)
            {
                return;
            }

            writer.Write(text);
            writer.Flush();
        }
    }
}
