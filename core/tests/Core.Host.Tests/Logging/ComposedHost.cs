using Core.Platform.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Logging;

/// <summary>The host's own composition root, built in process the one way every test of the log builds it.</summary>
internal static class ComposedHost
{
    /// <summary>The web host composed with the file destination given, after <paramref name="further"/> has had its say on the logging builder.</summary>
    public static WebApplication Build(ILoggerProvider? fileLog, Action<ILoggingBuilder>? further = null)
    {
        var builder = CompositionRoot.ClosedBuilder("http://127.0.0.1:0");
        CompositionRoot.Compose(builder, ClosedConfiguration.Load(new ConfigurationSchema([HostConfiguration.Section])).View, fileLog);
        further?.Invoke(builder.Logging);
        return builder.Build();
    }
}
