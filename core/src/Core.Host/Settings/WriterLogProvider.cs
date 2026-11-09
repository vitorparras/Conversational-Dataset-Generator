namespace Core.Host.Settings;

/// <summary>A logger provider that writes each line to the writer it is given, level first: the applier writes to its error output, which the service manager keeps as the log of the server.</summary>
internal sealed class WriterLogProvider : ILoggerProvider
{
    private readonly TextWriter writer;

    /// <summary>The provider over the writer.</summary>
    internal WriterLogProvider(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        this.writer = writer;
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new Lines(writer);

    /// <inheritdoc />
    public void Dispose()
    {
        // The writer belongs to whoever gave it.
    }

    private sealed class Lines : ILogger
    {
        private readonly TextWriter writer;

        internal Lines(TextWriter writer)
        {
            this.writer = writer;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            writer.WriteLine($"{logLevel}: {formatter(state, exception)}");
    }
}
