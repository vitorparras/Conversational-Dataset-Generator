namespace Core.Host.Settings;

/// <summary>A logger provider that writes each line to the writer it is given, level first: the applier writes to its error output, which the service manager keeps as the log of the server.</summary>
internal sealed class WriterLogProvider : ILoggerProvider
{
    private readonly Action<string> writeLine;

    /// <summary>The provider over the writer.</summary>
    internal WriterLogProvider(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writeLine = writer.WriteLine;
    }

    /// <inheritdoc />
    ILogger ILoggerProvider.CreateLogger(string categoryName) => new Lines(writeLine);

    /// <inheritdoc />
    public void Dispose()
    {
        // The writer belongs to whoever gave it.
    }

    private sealed class Lines : ILogger
    {
        private readonly Action<string> writeLine;

        internal Lines(Action<string> writeLine)
        {
            this.writeLine = writeLine;
        }

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        bool ILogger.IsEnabled(LogLevel logLevel) => true;

        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            writeLine($"{logLevel}: {formatter(state, exception)}");
    }
}
