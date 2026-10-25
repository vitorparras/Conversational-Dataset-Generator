using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Logging;

/// <summary>The host's own composition root, built in process, writing to a captured console.</summary>
internal static class ComposedConsole
{
    public const string Category = "Sample";

    /// <summary>What the console shows after the write, once the host and its logging pipeline are disposed.</summary>
    public static string LineWrittenBy(Action<ILogger> write)
    {
        using var console = new ConsoleCapture();
        using (var app = ComposedHost.Build(fileLog: null))
        {
            write(app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(Category));
        }

        return console.Text;
    }
}
