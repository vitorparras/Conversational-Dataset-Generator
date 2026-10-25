using System.Text.Json.Serialization;
using Core.Boundary.Contracts;
using Core.Boundary.Fake;
using Core.Boundary.Http;
using Core.Host.Analysis;
using Core.Host.Endpoints;
using Core.Host.Supervision;
using Core.Persistence.Connection;
using Core.Persistence.ServerGuarantees;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using Core.Platform.Logging;
using Core.Platform.Startup;
using Core.Platform.Versions;
using Npgsql;

namespace Core.Host;

/// <summary>The only place where the host is assembled: the target check first, then configuration, logging and services, then routes.</summary>
internal static class CompositionRoot
{
    /// <summary>How long the versions route waits for the service to give its model version, so a service that hangs does not hang the route.</summary>
    private static readonly TimeSpan ModelVersionTimeout = TimeSpan.FromSeconds(5);

    internal static async Task<HostLaunch> BuildAsync(string[] args)
    {
        var target = await new StartupRunner([TargetCheck()], TimeProvider.System).RunAsync(CancellationToken.None);
        if (!target.Ready)
        {
            return HostLaunch.CheckFailed(target);
        }

        var loaded = Load(args);
        var database = DatabaseConfiguration.Read(loaded.Values);
        if (loaded.Refused || database.State == DatabaseSettingsState.Refused)
        {
            return HostLaunch.Refused([.. loaded.Problems, .. database.Problems]);
        }

        var analyzeTimeout = LanguageServiceConfiguration.AnalyzeTimeout(loaded.Values);
        return analyzeTimeout is null
            ? HostLaunch.Refused([LanguageServiceConfiguration.AnalyzeTimeoutRefused(loaded.Values)])
            : await LaunchAsync(loaded, database, analyzeTimeout.Value, target);
    }

    /// <summary>Opens the file destination and the startup container, runs the checks and hands both to the host when every one passed; otherwise closes them here.</summary>
    private static async Task<HostLaunch> LaunchAsync(
        ConfigurationLoadResult loaded, DatabaseSettings database, TimeSpan analyzeTimeout, StartupReport target)
    {
        var fileLog = HostLogFile.Open(loaded);
        var startup = StartupServices(loaded, fileLog, database);
        try
        {
            IStartupCheck[] checks = [.. HostLogFile.Checks(loaded, fileLog), .. Checks(loaded, startup)];
            var report = await new StartupRunner(checks, startup.GetRequiredService<TimeProvider>()).RunAsync(CancellationToken.None);
            if (!report.Ready)
            {
                return HostLaunch.CheckFailed(report);
            }

            var launch = HostLaunch.Ready(Assemble(
                Prepared(loaded, fileLog, analyzeTimeout),
                new StartupReport([.. target.Results, .. report.Results], report.CheckedAt),
                startup,
                RecognizerConfiguration.KindOf(loaded.Values)));
            startup = null;
            fileLog = null;
            return launch;
        }
        finally
        {
            if (startup is not null)
            {
                await startup.DisposeAsync();
            }

            fileLog?.Dispose();
        }
    }

    /// <summary>The closed configuration of the host: the file beside it, the process environment and the strict arguments.</summary>
    private static ConfigurationLoadResult Load(string[] args) =>
        ClosedConfiguration.Load(
            new ConfigurationSchema([
                HostConfiguration.Section, LanguageServiceConfiguration.Section, RecognizerConfiguration.Section, DatabaseConfiguration.Section]),
            new JsonFileSource(HostConfiguration.SettingsPath),
            EnvironmentSource.FromProcess(),
            new StrictArgumentsSource(args));

    /// <summary>Logging with redaction first, then every service the routes consume and a JSON that refuses a field unknown, missing or null;
    /// the host's container owns the file destination and, since every logger depends on it, closes it after the last of them.</summary>
    internal static void Compose(IHostApplicationBuilder builder, EffectiveConfiguration configuration, ILoggerProvider? fileLog)
    {
        PlatformLogging.Configure(builder.Logging);
        if (fileLog is not null)
        {
            _ = builder.Services.AddSingleton(_ => fileLog);
        }

        _ = builder.Services.AddSingleton(TimeProvider.System);
        _ = builder.Services.AddRedaction(PlatformLogging.EraseSensitiveData);
        _ = builder.Services.AddSingleton(configuration);
        _ = builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            options.SerializerOptions.RespectRequiredConstructorParameters = true;
            options.SerializerOptions.RespectNullableAnnotations = true;
        });
    }

    /// <summary>The analysis and the recognizer the configuration chose: the fake by name, otherwise one per use, with the port and secret the supervisor has then.</summary>
    internal static void AddAnalysis(IServiceCollection services, IReadOnlyDictionary<string, string> values, TimeSpan analyzeTimeout)
    {
        _ = RecognizerConfiguration.UsesFake(values)
            ? services.AddSingleton<IRecognizer, FakeRecognizer>().AddSingleton<IModelVersionReader, NoModelVersionReader>()
            : services.AddTransient<IRecognizer>(provider => Recognizer(provider, analyzeTimeout))
                .AddTransient(ModelVersionReader);
        _ = services.AddTransient<MaterialAnalysis>();
    }

    /// <summary>The bundled file, the server checks, then the language service, which the fake recognizer does not ask and so is not started for it.</summary>
    private static IStartupCheck[] Checks(ConfigurationLoadResult loaded, IServiceProvider startup) =>
        RecognizerConfiguration.UsesFake(loaded.Values)
            ? [BundledFile(loaded), .. startup.GetServices<IStartupCheck>()]
            : [BundledFile(loaded), .. startup.GetServices<IStartupCheck>(), startup.GetRequiredService<LanguageServiceStartupCheck>()];

    /// <summary>Runs before the configuration is read: an executable built for another target is refused whatever it is configured with.</summary>
    private static RuntimeTargetCheck TargetCheck() =>
        new(BuildInfo.Of(typeof(CompositionRoot).Assembly).RuntimeIdentifier, RuntimeTarget.Current);

    private static BundledFileCheck BundledFile(ConfigurationLoadResult loaded) =>
        new(HostConfiguration.BundledFilePath, loaded.Values[HostConfiguration.BundledFilePath], AppContext.BaseDirectory);

    /// <summary>What the checks need before the host's own container exists: redacted logging, the server checks and the supervision of the
    /// language service. The host's container adopts it, so the service stops with the host; it only borrows the file destination.</summary>
    private static ServiceProvider StartupServices(ConfigurationLoadResult loaded, FileLoggerProvider? fileLog, DatabaseSettings database)
    {
        var services = new ServiceCollection();
        AddServerGuarantees(services, database);
        _ = services.AddSingleton(TimeProvider.System);
        _ = services.AddLogging(logging => WithFile(logging, fileLog));
        if (fileLog is not null)
        {
            _ = services.AddSingleton(fileLog);
        }

        _ = services.AddRedaction(PlatformLogging.EraseSensitiveData);
        _ = services.AddSingleton(_ => LoopbackHttpClient.Create());
        _ = services.AddSingleton(provider => new HealthWait(provider.GetRequiredService<HttpClient>()));
        _ = services.AddSingleton(new ServiceProcessLauncher(loaded.Values[LanguageServiceConfiguration.ExecutablePath]));
        _ = services.AddSingleton(new ServiceProcessWatch());
        _ = services.AddSingleton(provider => new LanguageServiceSupervisor(
            provider.GetRequiredService<ServiceProcessLauncher>(),
            provider.GetRequiredService<HealthWait>(),
            provider.GetRequiredService<ServiceProcessWatch>(),
            provider.GetRequiredService<ILogger<LanguageServiceSupervisor>>()));
        _ = services.AddSingleton(provider => new LanguageServiceStartupCheck(
            provider.GetRequiredService<LanguageServiceSupervisor>(), loaded.Values[LanguageServiceConfiguration.StartupTimeoutPath]));
        return services.BuildServiceProvider();
    }

    /// <summary>The platform's logging plus the file, registered as an instance so this container never closes it.</summary>
    private static void WithFile(ILoggingBuilder logging, ILoggerProvider? fileLog)
    {
        PlatformLogging.Configure(logging);
        if (fileLog is not null)
        {
            _ = logging.AddProvider(fileLog);
        }
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

    /// <summary>The web host with everything the configuration decides: its address, logging, the file destination and the recognizer.</summary>
    private static WebApplicationBuilder Prepared(ConfigurationLoadResult loaded, ILoggerProvider? fileLog, TimeSpan analyzeTimeout)
    {
        var builder = ClosedBuilder(loaded.Values[HostConfiguration.UrlsPath]);
        Compose(builder, loaded.View, fileLog);
        AddAnalysis(builder.Services, loaded.Values, analyzeTimeout);
        return builder;
    }

    /// <summary>What the checks produced, adopted by the host: the report the routes show, the startup container that stops the service with the host, and the recognizer in force.</summary>
    private static WebApplication Assemble(
        WebApplicationBuilder builder, StartupReport report, ServiceProvider startup, RecognizerKind recognizer)
    {
        _ = builder.Services.AddSingleton(report);
        _ = builder.Services.AddSingleton(provider => provider.GetRequiredService<ServiceProvider>().GetRequiredService<ServiceProcessWatch>());
        _ = builder.Services.AddSingleton(_ => startup);
        var versions = new VersionReport(BuildInfo.Of(typeof(CompositionRoot).Assembly));
        _ = builder.Services.AddSingleton(versions);

        var app = builder.Build();
        _ = app.Services.GetRequiredService<ServiceProvider>();
        app.Logger.VersionsInForce(versions);
        StatusEndpoint.Map(app, recognizer);
        ConfigurationEndpoint.Map(app);
        VersionsEndpoint.Map(app);
        AnalysisEndpoint.Map(app);
        return app;
    }

    /// <summary>The recognizer of the service the supervisor started, with its secret and the client that never goes through a proxy.</summary>
    private static HttpRecognizer Recognizer(IServiceProvider host, TimeSpan analyzeTimeout)
    {
        var startup = host.GetRequiredService<ServiceProvider>();
        var supervisor = startup.GetRequiredService<LanguageServiceSupervisor>();
        var port = supervisor.Port ?? throw new InvalidOperationException("The language service has no port before it has answered.");
        return new HttpRecognizer(
            startup.GetRequiredService<HttpClient>(),
            new HttpRecognizerSettings(port, supervisor.Secret, analyzeTimeout),
            host.GetRequiredService<ILogger<HttpRecognizer>>());
    }

    /// <summary>The reader of the model version of the service the supervisor has now; with no service running there is none to ask.</summary>
    private static IModelVersionReader ModelVersionReader(IServiceProvider host)
    {
        var startup = host.GetRequiredService<ServiceProvider>();
        var supervisor = startup.GetRequiredService<LanguageServiceSupervisor>();
        return supervisor.Port is { } port && supervisor.Secret.Length > 0
            ? new HttpModelVersionReader(startup.GetRequiredService<HttpClient>(), new HttpRecognizerSettings(port, supervisor.Secret, ModelVersionTimeout))
            : new NoModelVersionReader();
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
