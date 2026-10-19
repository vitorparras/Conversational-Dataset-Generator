namespace Core.Platform.Logging;

/// <summary>
/// The open log file: one writer at a time, each text flushed before the call returns, nothing written once closed.
/// A write the file refuses closes it and says so once on the notice; whoever logged never sees the failure.
/// </summary>
internal sealed class LogFile : IDisposable
{
    private const string Stopped =
        "O registro em arquivo parou: a escrita no arquivo falhou (disco cheio ou arquivo indisponível). "
        + "O registro segue só no console.";

    private readonly Lock gate = new();
    private readonly StreamWriter writer;
    private readonly Action<string> notice;
    private bool closed;

    internal LogFile(StreamWriter writer, Action<string> notice)
    {
        this.writer = writer;
        this.notice = notice;
    }

    /// <summary>Closes the file; what is appended afterwards is dropped, never thrown.</summary>
    public void Dispose()
    {
        lock (gate)
        {
            closed = true;
            try
            {
                writer.Dispose();
            }
            catch (IOException)
            {
                // What a refused write left in the buffer cannot reach the file either; the handle is released all the same.
            }
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

            try
            {
                writer.Write(text);
                writer.Flush();
            }
            catch (IOException)
            {
                Dispose();
                notice(Stopped);
            }
        }
    }
}
