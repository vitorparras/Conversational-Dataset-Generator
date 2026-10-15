using System.Text.Json.Serialization;
using Core.Host.Endpoints;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;

namespace Core.Host;

/// <summary>The only place where the host is assembled: configuration first, then services, then routes.</summary>
internal static class CompositionRoot
{
    public static HostLaunch Build(string[] args)
    {
        var loaded = ClosedConfiguration.Load(
            new ConfigurationSchema([HostConfiguration.Section]),
            new JsonFileSource(HostConfiguration.SettingsFile),
            EnvironmentSource.FromProcess(),
            new StrictArgumentsSource(args));
        return loaded.Refused ? HostLaunch.Refused(loaded.Problems) : HostLaunch.Ready(Assemble(loaded));
    }

    private static WebApplication Assemble(ConfigurationLoadResult loaded)
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseUrls(loaded.Values[HostConfiguration.UrlsPath]);
        _ = builder.Services.AddSingleton(loaded.View);
        _ = builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        var app = builder.Build();
        StatusEndpoint.Map(app);
        ConfigurationEndpoint.Map(app);
        return app;
    }
}
