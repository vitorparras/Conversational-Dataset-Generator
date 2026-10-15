using System.Text.Json.Serialization;
using Core.Host.Endpoints;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using Core.Platform.Logging;

namespace Core.Host;

/// <summary>The only place where the host is assembled: configuration first, then logging and services, then routes.</summary>
internal static class CompositionRoot
{
    public static HostLaunch Build(string[] args)
    {
        var loaded = ClosedConfiguration.Load(
            new ConfigurationSchema([HostConfiguration.Section]),
            new JsonFileSource(HostConfiguration.SettingsPath),
            EnvironmentSource.FromProcess(),
            new StrictArgumentsSource(args));
        return loaded.Refused ? HostLaunch.Refused(loaded.Problems) : HostLaunch.Ready(Assemble(loaded));
    }

    /// <summary>Logging with redaction before anything else, then every service the routes consume.</summary>
    public static void Compose(WebApplicationBuilder builder, EffectiveConfiguration configuration)
    {
        PlatformLogging.Configure(builder.Logging);
        _ = builder.Services.AddRedaction(PlatformLogging.EraseSensitiveData);
        _ = builder.Services.AddSingleton(configuration);
        _ = builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    }

    private static WebApplication Assemble(ConfigurationLoadResult loaded)
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseUrls(loaded.Values[HostConfiguration.UrlsPath]);
        Compose(builder, loaded.View);

        var app = builder.Build();
        StatusEndpoint.Map(app);
        ConfigurationEndpoint.Map(app);
        return app;
    }
}
