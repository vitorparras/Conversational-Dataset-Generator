using Core.Platform.Configuration;
using Core.Platform.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Privacy;

/// <summary>The host's composition root, built in process with the file destination open; what comes back is the type of every logger provider it holds.</summary>
internal static class ComposedProviders
{
    /// <summary>The types the composed host writes its log through, after <paramref name="further"/> has had its say on the logging builder.</summary>
    public static IReadOnlyList<Type> TypesOf(Action<ILoggingBuilder> further)
    {
        using var directory = new TemporaryDirectory();
        using var fileLog = FileLoggerProvider.Open(Path.Combine(directory.Path, "ZQX-LOG-0028.log"), new LineConsoleFormatter(TimeProvider.System), TextWriter.Null);
        var builder = CompositionRoot.ClosedBuilder("http://127.0.0.1:0");
        CompositionRoot.Compose(builder, ClosedConfiguration.Load(new ConfigurationSchema([HostConfiguration.Section])).View, fileLog);
        further(builder.Logging);
        using var app = builder.Build();
        return [.. app.Services.GetServices<ILoggerProvider>().Select(provider => provider.GetType())];
    }
}
