using System.Text.Json.Serialization;
using Core.Host.Endpoints;
using Core.Host.Supervision;
using Core.Persistence.Connection;
using Core.Persistence.ServerGuarantees;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using Core.Platform.Logging;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host;

/// <summary>The only place where the host is assembled: configuration first, then logging and services, then routes.</summary>
internal static class CompositionRoot
{
    internal static async Task<HostLaunch> BuildAsync(string[] args)
    {
        var loaded = ClosedConfiguration.Load(
            new ConfigurationSchema([HostConfiguration.Section, LanguageServiceConfiguration.Section, DatabaseConfiguration.Section]),
            new JsonFileSource(HostConfiguration.SettingsPath),
            EnvironmentSource.FromProcess(),
            new StrictArgumentsSource(args));
        var database = DatabaseConfiguration.Read(loaded.Values);
        if (loaded.Refused || database.State == DatabaseSettingsState.Refused)
        {
            return HostLaunch.Refused([.. loaded.Problems, .. database.Problems]);
        }

        var startup = StartupServices(loaded, database);
        try
        {
            IStartupCheck[] checks =
                [BundledFile(loaded), .. startup.GetServices<IStartupCheck>(), startup.GetRequiredService<LanguageServiceStartupCheck>()];
            var report = await new StartupRunner(checks, startup.GetRequiredService<TimeProvider>()).RunAsync(CancellationToken.None);
            if (!report.Ready)
            {
                return HostLaunch.CheckFailed(report);
            }

            var launch = HostLaunch.Ready(Assemble(loaded, report, startup));
            startup = null;
            return launch;
        }
        finally
        {
            if (startup is not null)
            {
                await startup.DisposeAsync();
            }
        }
    }

    /// <summary>Logging with redaction before anything else, then every service the routes consume.</summary>
    internal static void Compose(IHostApplicationBuilder builder, EffectiveConfiguration configuration)
    {
        PlatformLogging.Configure(builder.Logging);
        _ = builder.Services.AddSingleton(TimeProvider.System);
        _ = builder.Services.AddRedaction(PlatformLogging.EraseSensitiveData);
        _ = builder.Services.AddSingleton(configuration);
        _ = builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    }

    private static BundledFileCheck BundledFile(ConfigurationLoadResult loaded) =>
        new(HostConfiguration.BundledFilePath, loaded.Values[HostConfiguration.BundledFilePath], AppContext.BaseDirectory);

    /// <summary>
    /// What the checks need before the host's own container exists: redacted logging, any server checks, and the supervision of
    /// the language service. The host's container adopts it, so the service stops when the host does.
    /// </summary>
    private static ServiceProvider StartupServices(ConfigurationLoadResult loaded, DatabaseSettings database)
    {
        var services = new ServiceCollection();
        AddServerGuarantees(services, database);
        _ = services.AddSingleton(TimeProvider.System);
        _ = services.AddLogging(PlatformLogging.Configure);
        _ = services.AddRedaction(PlatformLogging.EraseSensitiveData);
        _ = services.AddSingleton(_ => HealthWait.NewClient());
        _ = services.AddSingleton(provider => new HealthWait(provider.GetRequiredService<HttpClient>()));
        _ = services.AddSingleton(new ServiceProcessLauncher(loaded.Values[LanguageServiceConfiguration.ExecutablePath]));
        _ = services.AddSingleton(provider => new LanguageServiceSupervisor(
            provider.GetRequiredService<ServiceProcessLauncher>(),
            provider.GetRequiredService<HealthWait>(),
            provider.GetRequiredService<ILogger<LanguageServiceSupervisor>>()));
        _ = services.AddSingleton(provider => new LanguageServiceStartupCheck(
            provider.GetRequiredService<LanguageServiceSupervisor>(), loaded.Values[LanguageServiceConfiguration.StartupTimeoutPath]));
        return services.BuildServiceProvider();
    }

    /// <summary>The application's data source and the five server checks, privilege first, or the check that says no database is declared.</summary>
    private static void AddServerGuarantees(IServiceCollection services, DatabaseSettings database)
    {
        if (database.State != DatabaseSettingsState.Configured)
        {
            _ = services.AddSingleton<IStartupCheck>(new UnconfiguredDatabaseCheck());
            return;
        }

        _ = services.AddSingleton(_ => NpgsqlDataSource.Create(database.ConnectionString!));
        _ = services.AddSingleton<IStartupCheck>(provider => new PrivilegeCheck(provider.GetRequiredService<NpgsqlDataSource>()));
        _ = services.AddSingleton<IStartupCheck>(provider => new RlsForceCheck(provider.GetRequiredService<NpgsqlDataSource>()));
        _ = services.AddSingleton<IStartupCheck>(provider => new SerializableCheck(provider.GetRequiredService<NpgsqlDataSource>()));
        _ = services.AddSingleton<IStartupCheck>(provider => new PgAuditCheck(provider.GetRequiredService<NpgsqlDataSource>()));
        _ = services.AddSingleton<IStartupCheck>(provider => new ColumnRevokeCheck(provider.GetRequiredService<NpgsqlDataSource>()));
    }

    private static WebApplication Assemble(ConfigurationLoadResult loaded, StartupReport report, ServiceProvider startup)
    {
        var builder = ClosedBuilder(loaded.Values[HostConfiguration.UrlsPath]);
        Compose(builder, loaded.View);
        _ = builder.Services.AddSingleton(report);
        _ = builder.Services.AddSingleton(_ => startup);

        var app = builder.Build();
        _ = app.Services.GetRequiredService<ServiceProvider>();
        StatusEndpoint.Map(app);
        ConfigurationEndpoint.Map(app);
        return app;
    }

    /// <summary>A web host with no configuration source of its own, so nothing reaches it except what the closed configuration approved.</summary>
    internal static WebApplicationBuilder ClosedBuilder(string urls)
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
        _ = builder.WebHost.UseKestrel().UseUrls(urls);
        _ = builder.Services.AddRoutingCore();
        _ = builder.Services.Configure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true);
        return builder;
    }
}
