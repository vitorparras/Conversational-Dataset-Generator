using System.Text;

namespace Core.Host.Supervision;

/// <summary>
/// The end of what the service wrote to its standard error: the last characters up to a limit, drained as the service writes so the pipe never fills and holds it.
/// The text is the service's own and may echo the material it was given, so it is for whoever asked to collect it and never for the log.
/// </summary>
internal sealed class ErrorTail
{
    private readonly Lock gate = new();
    private readonly StringBuilder text = new();
    private readonly int limit;
    private readonly Task drained;

    internal ErrorTail(TextReader reader, int limit)
    {
        this.limit = limit;
        drained = DrainAsync(reader);
    }

    /// <summary>What was read so far, at most the limit.</summary>
    internal string Text
    {
        get
        {
            lock (gate)
            {
                return text.ToString();
            }
        }
    }

    /// <summary>The end of the text once the service closed its standard error, or what was read when the wait ran out: a process of its tree may still hold the pipe open.</summary>
    internal async Task<string> TextAsync(TimeSpan wait)
    {
        _ = await Task.WhenAny(drained, Task.Delay(wait));
        return Text;
    }

    private void Append(char[] buffer, int count)
    {
        lock (gate)
        {
            _ = text.Append(buffer, 0, count);
            if (text.Length > limit)
            {
                _ = text.Remove(0, text.Length - limit);
            }
        }
    }

    private async Task DrainAsync(TextReader reader)
    {
        var buffer = new char[1024];
        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer)) > 0)
            {
                Append(buffer, read);
            }
        }
        catch (Exception ended) when (ended is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // The process is gone or released; what was read is what there is.
        }
    }
}
