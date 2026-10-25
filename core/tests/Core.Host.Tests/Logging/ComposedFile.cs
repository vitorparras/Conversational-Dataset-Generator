using Core.Platform.Configuration;
using Core.Platform.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Logging;

/// <summary>The host's own composition root, built in process with the file destination open on a temporary path.</summary>
internal sealed class ComposedFile : IDisposable
{
    public const string Category = "Sample";
    private readonly TemporaryDirectory directory = new();

    public ComposedFile()
    {
        Path = System.IO.Path.Combine(directory.Path, "ZQX-LOG-7731.log");
    }

    public string Path { get; }

    /// <summary>Whether the file was closed once the write was done and the host disposed.</summary>
    public bool ClosedByTheHost { get; private set; }

    /// <summary>What the console showed during the last write.</summary>
    public string ConsoleText { get; private set; } = string.Empty;

    /// <summary>What the file holds after the write, once the host and its logging pipeline are disposed.</summary>
    public string TextWrittenBy(Action<ILogger> log)
    {
        using var console = new ConsoleCapture();
        using var fileLog = FileLoggerProvider.Open(Path, new LineConsoleFormatter(TimeProvider.System), TextWriter.Null);
        using (var app = Build(fileLog))
        {
            log(app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(Category));
        }

        return Closed(console);
    }

    /// <summary>The same, for work that needs the host's services, such as the loggers of its own types, and awaits.</summary>
    public async Task<string> TextWrittenByAsync(Func<IServiceProvider, Task> use)
    {
        using var console = new ConsoleCapture();
        using var fileLog = FileLoggerProvider.Open(Path, new LineConsoleFormatter(TimeProvider.System), TextWriter.Null);
        using (var app = Build(fileLog))
        {
            await use(app.Services);
        }

        return Closed(console);
    }

    public void Dispose() => directory.Dispose();

    private static WebApplication Build(ILoggerProvider? fileLog)
    {
        var builder = CompositionRoot.ClosedBuilder("http://127.0.0.1:0");
        CompositionRoot.Compose(builder, ClosedConfiguration.Load(new ConfigurationSchema([HostConfiguration.Section])).View, fileLog);
        return builder.Build();
    }

    private string Closed(ConsoleCapture console)
    {
        ConsoleText = console.Text;
        ClosedByTheHost = IsClosed();
        return File.ReadAllText(Path);
    }

    private bool IsClosed()
    {
        try
        {
            using var stream = new FileStream(Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
