// The order is fixed and written once, here: configuration, log path, versions, startup checks, port.
// Nothing on this path reaches the network (requirement 022).
using System.Globalization;
using System.Net;
using System.Text;
using Core.Host;
using Core.Host.Endpoints;
using Core.Platform.Configuration;
using Core.Platform.Logging;
using Core.Platform.Startup;
using Core.Platform.Versioning;

// Log lines and refusals carry Portuguese text; whoever reads the output reads UTF-8 on every system.
Console.OutputEncoding = Encoding.UTF8;

// 1. Configuration. Refused before anything else exists, so it goes to standard error.
var schema = new ConfigurationSchema([HostConfiguration.Server, PlatformLogging.Section, BundledFileCheck.Section]);
var load = ClosedConfiguration.Load(ConfigurationSources.Standard(AppContext.BaseDirectory, args), schema);
if (load.Configuration is not { } configuration)
{
    await Console.Error.WriteLineAsync("Configuração recusada; o núcleo não inicia:");
    foreach (var problem in load.Problems)
    {
        await Console.Error.WriteLineAsync("  " + problem.Describe());
    }

    return ExitCodes.ConfigurationRefused;
}

// 2. Log path, with redaction, before the first line is written.
var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
builder.Services.AddPlatformLogging(configuration);
builder.WebHost.UseKestrelCore();
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(
    IPAddress.Parse(configuration.Get(HostConfiguration.Address)),
    int.Parse(configuration.Get(HostConfiguration.Port), CultureInfo.InvariantCulture)));
builder.Services.AddRoutingCore();

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Core.Host");
var exitCode = ExitCodes.Requested;
try
{
    // The versions go to the log first, so they outlive the process (issue 51 CA-3).
    var versions = VersionReport.FromBuild();
    if (logger.IsEnabled(LogLevel.Information))
    {
        logger.Versions(versions.Describe());
    }

    // 3. Startup checks, in this order, before the port opens.
    IReadOnlyList<IStartupCheck> checks = [.. BundledFileCheck.FromConfiguration(configuration, AppContext.BaseDirectory)];
    var report = await StartupRunner.RunAsync(checks, logger, CancellationToken.None);
    if (!report.AllPassed)
    {
        var failed = report.Results.Count(result => result.Status == CheckStatus.Failed);
        logger.StartupRefused(failed);
        exitCode = ExitCodes.StartupCheckFailed;
        return exitCode;
    }

    // 4. Port.
    StatusEndpoint.Map(app, report);
    ConfigurationEndpoint.Map(app, configuration);
    VersionsEndpoint.Map(app, versions);
    await app.StartAsync();
    if (logger.IsEnabled(LogLevel.Information))
    {
        logger.Listening(string.Join(", ", app.Urls));
    }

    // Ctrl+C or SIGTERM: the host stops accepting, finishes the requests in progress, then returns.
    await app.WaitForShutdownAsync();
    logger.ShutdownRequested();
    return exitCode;
}
#pragma warning disable CA1031 // The last line of defence: log the type, never the message, and exit with failure.
catch (Exception exception)
#pragma warning restore CA1031
{
    exitCode = ExitCodes.Failure;
    logger.UnexpectedFailure(exception, exitCode);
    return exitCode;
}
finally
{
    // Disposing drains the console queue and closes the file: the last line is on disk.
    logger.ShutdownCompleted(exitCode);
    await app.DisposeAsync();
}
