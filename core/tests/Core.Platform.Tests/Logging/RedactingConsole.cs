using Core.Platform.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Platform.Tests.Logging;

/// <summary>The logging pipeline exactly as the host composes it, writing to a captured console.</summary>
internal static class RedactingConsole
{
    public const string Category = "Sample";

    /// <summary>What the console shows after the write, once the pipeline has flushed.</summary>
    public static string LineWrittenBy(Action<ILogger> write)
    {
        using var console = new ConsoleCapture();
        using (var provider = Build())
        {
            write(provider.GetRequiredService<ILoggerFactory>().CreateLogger(Category));
        }

        return console.Text;
    }

    private static ServiceProvider Build() =>
        new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddLogging(PlatformLogging.Configure)
            .AddRedaction(PlatformLogging.EraseSensitiveData)
            .BuildServiceProvider();
}
