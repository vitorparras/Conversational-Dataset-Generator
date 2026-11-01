using Core.Boundary.Contracts;
using Core.Boundary.Fake;
using Core.Boundary.Http;
using Core.Host.Analysis;
using Core.Host.Endpoints;
using Core.Host.Migration;
using Core.Host.Supervision;
using Core.Persistence.Connection;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.ServerGuarantees;
using Core.Platform.Configuration;
using Core.Platform.Keys;
using Core.Platform.Logging;
using Core.Platform.Sessions;
using Core.Platform.Startup;
using Core.Platform.Versions;
using Npgsql;

namespace Core.Host;

/// <summary>The only place where the host is assembled: the target check first, then configuration, logging and services, then routes.</summary>
internal static class CompositionRoot
{
    internal static async Task<HostLaunch> BuildAsync(string[] args)
    {
        var target = await new StartupRunner([HostConfiguration.TargetCheck()], TimeProvider.System).RunAsync(CancellationToken.None);
        if (!target.Ready)
        {
            return HostLaunch.CheckFailed(target);
        }

        var migrating = MigrationMode.Requested(args);
        var loaded = HostConfiguration.Load(MigrationMode.ConfigurationArguments(args), migrating);
        var database = HostConfiguration.DatabaseOf(loaded, migrating);
        if (database is null || database.State == DatabaseSettingsState.Refused)
        {
            return HostLaunch.Refused(database?.Problems ?? loaded.Problems);
        }

        if (migrating)
        {
            return PasswordConfiguration.Refusals(loaded.Values) is { Count: > 0 } invalid ? HostLaunch.Refused(invalid) : await MigrateAsync(database);
        }

        var analyzeTimeout = LanguageServiceConfiguration.AnalyzeTimeout(loaded.Values);
        var refused = HostConfiguration.Refusals(loaded.Values, analyzeTimeout);
        return analyzeTimeout is { } timeout && refused.Count == 0 ? await LaunchAsync(loaded, database, timeout, target) : HostLaunch.Refused(refused);
    }

    /// <summary>The migration mode: the steps run once with the owner credential the configuration holds, the outcome is logged and nothing is served.</summary>
    private static async Task<HostLaunch> MigrateAsync(DatabaseSettings database)
    {
        await using var dataSource = NpgsqlDataSource.Create(database.ConnectionString!);
        await using var logging = MigrationServices();
        var result = await new MigrationRunner(dataSource, EmbeddedMigrations.Load()).RunAsync(CancellationToken.None);
        logging.GetRequiredService<ILogger<MigrationRunner>>().Write(result);
        return HostLaunch.Migrated(result.Outcome);
    }

    /// <summary>Opens the file destination and the startup container, runs the checks and hands both to the host when every one passed; otherwise closes them here.</summary>
    private static async Task<HostLaunch> LaunchAsync(ConfigurationLoadResult loaded, DatabaseSettings database, TimeSpan analyzeTimeout, StartupReport target)
    {
        var fileLog = HostLogFile.Open(loaded);
        var startup = StartupServices(loaded, fileLog, database);
        try
        {
            IStartupCheck[] checks = [.. HostLogFile.Checks(loaded, fileLog), .. StartupChecks.Of(loaded, startup)];
            var report = await new StartupRunner(checks, startup.GetRequiredService<TimeProvider>()).RunAsync(CancellationToken.None);
            if (!report.Ready)
            {
                return HostLaunch.CheckFailed(report);
            }

            var launch = HostLaunch.Ready(await AssembleAsync(
                TlsConfiguration.Serve(Prepared(loaded, fileLog, analyzeTimeout, checks), startup),
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

    /// <summary>Logging with redaction first, then the services the routes consume and a JSON that refuses a field unknown, missing or null; the container owns the file destination.</summary>
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
        _ = builder.Services.ConfigureHttpJsonOptions(HostJson.Strict);
    }

    /// <summary>The analysis and the recognizer the configuration chose: the fake by name, otherwise one per use, with the port and secret the supervisor has then.</summary>
    internal static void AddAnalysis(IServiceCollection services, IReadOnlyDictionary<string, string> values, TimeSpan analyzeTimeout)
    {
        _ = RecognizerConfiguration.UsesFake(values)
            ? services.AddSingleton<IRecognizer, FakeRecognizer>().AddSingleton<IServiceVersionReader, NoServiceVersionReader>()
            : services.AddTransient(provider => Recognizer(provider, analyzeTimeout))
                .AddTransient(ServiceVersionReader);
        _ = services.AddTransient<MaterialAnalysis>();
    }

    /// <summary>What the checks need before the host's container exists: redacted logging, the server checks and the language service's supervision, which the host then adopts.</summary>
    private static ServiceProvider StartupServices(ConfigurationLoadResult loaded, FileLoggerProvider? fileLog, DatabaseSettings database)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IMasterKeySource>(MasterKeyConfiguration.SourceOf(loaded.Values));
        _ = services.AddSingleton<IStartupCheck>(provider => new MasterKeyCheck(provider.GetRequiredService<IMasterKeySource>()));
        _ = services.AddSingleton(_ => new TlsMaterialCheck(TlsConfiguration.SourceOf(loaded.Values)))
            .AddSingleton<IStartupCheck>(provider => provider.GetRequiredService<TlsMaterialCheck>());
        AddServerGuarantees(services, database);
        _ = services.AddSingleton(TimeProvider.System);
        _ = services.AddLogging(logging => HostLogFile.WithFile(logging, fileLog));
        if (fileLog is not null)
        {
            _ = services.AddSingleton(fileLog);
        }

        _ = services.AddRedaction(PlatformLogging.EraseSensitiveData);
        _ = services.AddSingleton(_ => LoopbackHttpClient.Create());
        _ = services.AddSingleton(provider => new HealthWait(provider.GetRequiredService<HttpClient>()));
        _ = services.AddSingleton(new ServiceProcessLauncher(ServiceFolder.Beside().Executable(LanguageServiceConfiguration.GivenExecutable(loaded.Values))));
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

    /// <summary>Redacted console logging alone: the migration mode has no host, no file destination and no language service.</summary>
    private static ServiceProvider MigrationServices()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TimeProvider.System);
        _ = services.AddLogging(PlatformLogging.Configure);
        _ = services.AddRedaction(PlatformLogging.EraseSensitiveData);
        return services.BuildServiceProvider();
    }

    /// <summary>The application's data source and the nine server checks, privilege first and the derivation labels last, or the check that says no database is declared.</summary>
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
        _ = services.AddSingleton<IStartupCheck>(provider => new SchemaVersionCheck(provider.GetRequiredService<NpgsqlDataSource>(), EmbeddedMigrations.Load()));
        _ = services.AddSingleton<IStartupCheck>(provider => new ImmutabilityCheck(provider.GetRequiredService<NpgsqlDataSource>()));
        _ = services.AddSingleton<IStartupCheck>(provider => new ProtectedColumnCheck(provider.GetRequiredService<NpgsqlDataSource>()));
        _ = services.AddSingleton<IStartupCheck>(provider => new DerivationLabelCheck(provider.GetRequiredService<NpgsqlDataSource>(), provider.GetRequiredService<IMasterKeySource>()));
    }

    /// <summary>The web host with everything the configuration decides: its address, logging, the file destination, the recognizer and the versions the checks confirmed.</summary>
    private static WebApplicationBuilder Prepared(ConfigurationLoadResult loaded, ILoggerProvider? fileLog, TimeSpan analyzeTimeout, System.Collections.IEnumerable checks)
    {
        var builder = ClosedBuilder(loaded.Values[HostConfiguration.UrlsPath]);
        _ = builder.Services.Configure<CookiePolicyOptions>(TransportSecurity.CookiePolicy);
        Compose(builder, loaded.View, fileLog);
        AddAnalysis(builder.Services, loaded.Values, analyzeTimeout);
        _ = builder.Services.AddSingleton(PasswordConfiguration.Derivation(loaded.Values));
        _ = builder.Services.AddSingleton(provider => new SessionStore(SessionConfiguration.Lifetimes(loaded.Values), provider.GetRequiredService<TimeProvider>()));
        _ = builder.Services.AddSingleton(InstallationConfiguration.Of(loaded.Values)).AddSingleton(provider => InstallationConfiguration.Shown(StartupVersions.Of(checks), provider));
        if (SafekeepingConfiguration.Declaration(loaded.Values) is { } declaration)
        {
            _ = builder.Services.AddSingleton(declaration);
        }

        return builder;
    }

    /// <summary>What the checks produced, adopted by the host: the report, the startup container, the recognizer in force and, once, the service's versions for the startup line.</summary>
    private static async Task<WebApplication> AssembleAsync(WebApplicationBuilder builder, StartupReport report, ServiceProvider startup, RecognizerKind recognizer)
    {
        _ = builder.Services.AddSingleton(report);
        _ = builder.Services.AddSingleton(provider => provider.GetRequiredService<ServiceProvider>().GetRequiredService<ServiceProcessWatch>());
        _ = builder.Services.AddSingleton(_ => startup);
        _ = builder.Services.AddSingleton(provider => provider.GetRequiredService<ServiceProvider>().GetService<NpgsqlDataSource>() is { } source
            ? new SignIn(new PersonRegistry(source), new PersonCredentials(source, provider.GetRequiredService<PasswordDerivation>()), provider.GetRequiredService<SessionStore>())
            : null!);

        var app = builder.Build();
        _ = app.Services.GetRequiredService<ServiceProvider>();
        await StartupVersions.WriteAsync(
            app.Logger, app.Services.GetRequiredService<VersionReport>(), app.Services.GetRequiredService<IServiceVersionReader>(), CancellationToken.None);
        InstallationConfiguration.Announce(app.Logger, app.Services);
        HostRoutes.Map(app, recognizer);
        return app;
    }

    /// <summary>The recognizer of the service the supervisor has alive now, with its secret and the client that never goes through a proxy;
    /// with no such service, one that refuses without connecting, so that nothing goes to the port a service that is gone once held.</summary>
    private static IRecognizer Recognizer(IServiceProvider host, TimeSpan analyzeTimeout)
    {
        var startup = host.GetRequiredService<ServiceProvider>();
        return startup.GetRequiredService<LanguageServiceSupervisor>().Endpoint is { } endpoint
            ? new HttpRecognizer(
                startup.GetRequiredService<HttpClient>(),
                new HttpRecognizerSettings(endpoint.Port, endpoint.Secret, analyzeTimeout),
                host.GetRequiredService<ILogger<HttpRecognizer>>())
            : new StoppedServiceRecognizer(host.GetRequiredService<ILogger<StoppedServiceRecognizer>>());
    }

    /// <summary>The reader of the versions of the service the supervisor has now; with no service running there is none to ask.</summary>
    private static IServiceVersionReader ServiceVersionReader(IServiceProvider host)
    {
        var startup = host.GetRequiredService<ServiceProvider>();
        return startup.GetRequiredService<LanguageServiceSupervisor>().Endpoint is { } endpoint
            ? new HttpServiceVersionReader(startup.GetRequiredService<HttpClient>(), new HttpRecognizerSettings(endpoint.Port, endpoint.Secret, LanguageServiceConfiguration.VersionsTimeout))
            : new NoServiceVersionReader();
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
