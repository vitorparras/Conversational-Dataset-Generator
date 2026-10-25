using Core.Platform.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Platform.Tests.Logging;

/// <summary>The logging pipeline as the host composes it, writing to a captured console and to a file in a temporary directory.</summary>
internal sealed class FilePipeline : IDisposable
{
    public const string Category = "Sample";
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 34, 56, 789, TimeSpan.Zero);
    private readonly Sources.TemporaryDirectory directory = new();

    public FilePipeline()
    {
        Path = System.IO.Path.Combine(directory.Path, "ZQX-MARKER-7731.log");
    }

    public string Path { get; }

    /// <summary>The file destination opened on this pipeline's path, stamping lines with the same fixed clock as the console.</summary>
    public FileLoggerProvider Open() => FileLoggerProvider.Open(Path, new LineConsoleFormatter(new FixedTimeProvider(Now)), TextWriter.Null)!;

    /// <summary>What the console and the file hold after the write, once the pipeline and the file are closed.</summary>
    public WrittenText Logged(Action<ILogger> log)
    {
        using var console = new ConsoleCapture();
        using (var file = Open())
        {
            using var provider = Build(file);
            log(provider.GetRequiredService<ILoggerFactory>().CreateLogger(Category));
        }

        return new WrittenText(console.Text, File.ReadAllText(Path));
    }

    public void Dispose() => directory.Dispose();

    /// <summary>The pipeline borrowing the file, as the startup container does; disposing it leaves the file open.</summary>
    public static ServiceProvider Build(ILoggerProvider file) =>
        new ServiceCollection()
            .AddSingleton<TimeProvider>(new FixedTimeProvider(Now))
            .AddLogging(logging =>
            {
                PlatformLogging.Configure(logging);
                _ = logging.AddProvider(file);
            })
            .AddRedaction(PlatformLogging.EraseSensitiveData)
            .BuildServiceProvider();
}
