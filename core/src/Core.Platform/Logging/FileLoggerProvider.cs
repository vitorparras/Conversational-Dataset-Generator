using System.Text;
using Core.Platform.Time;
using Microsoft.Extensions.Logging;

namespace Core.Platform.Logging;

/// <summary>
/// Appends log lines to a file. Each line is flushed as it is written, so the last line before a
/// shutdown or a crash is on disk.
/// </summary>
internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly Lock gate = new();
    private readonly StreamWriter writer;
    private readonly IClock clock;

    public FileLoggerProvider(string path, IClock clock)
    {
        this.clock = clock;
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is not null)
        {
            _ = Directory.CreateDirectory(directory);
        }

        var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
        lock (gate)
        {
            writer.Flush();
            writer.Dispose();
        }
    }

    private void Write(string line)
    {
        lock (gate)
        {
            writer.WriteLine(line);
            writer.Flush();
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider provider;
        private readonly string category;

        public FileLogger(FileLoggerProvider provider, string category)
        {
            this.provider = provider;
            this.category = category;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            provider.Write(LogLine.Format(provider.clock.UtcNow, logLevel, category, eventId, message, exception));
        }
    }
}
