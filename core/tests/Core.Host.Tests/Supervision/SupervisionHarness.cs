using System.Net;
using Core.Boundary.Http;
using Core.Host.Supervision;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Supervision;

/// <summary>The pieces the supervision is built from, wired the way the host wires them; whatever a test starts through it is stopped on dispose.</summary>
internal sealed class SupervisionHarness : IDisposable
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    private readonly List<IDisposable> started = [];

    public HealthWait Health => new(Client);

    /// <summary>The client the host hands the supervision and the recognizer: it never goes through a proxy.</summary>
    public HttpClient Client { get; } = LoopbackHttpClient.Create();

    /// <summary>Every line the supervisors made here have written.</summary>
    public RecordingLogger Log { get; } = new();

    public static ServiceProcessLauncher Launcher => new(LanguageServiceInterpreter.Path, new ErrorCollector());

    /// <summary>A secret no file on the machine can hold before the test makes it.</summary>
    public static string NewSecret() => $"ZQX-MARKER-7731-{Guid.NewGuid():N}";

    public LanguageServiceSupervisor SupervisorFor(string executable) => SupervisorFor(executable, new ServiceProcessWatch(), Log);

    public LanguageServiceSupervisor SupervisorFor(string executable, ServiceProcessWatch watch) => SupervisorFor(executable, watch, Log);

    /// <summary>A supervisor that writes its lines to the logger given, so a test can see when each was written.</summary>
    public LanguageServiceSupervisor SupervisorFor(string executable, ILogger<LanguageServiceSupervisor> logger) =>
        SupervisorFor(executable, new ServiceProcessWatch(), logger);

    public LanguageServiceSupervisor SupervisorFor(string executable, ServiceProcessWatch watch, ILogger<LanguageServiceSupervisor> logger)
    {
        var supervisor = new LanguageServiceSupervisor(new ServiceProcessLauncher(executable, new ErrorCollector()), Health, watch, logger);
        started.Add(supervisor);
        return supervisor;
    }

    /// <summary>A supervisor whose service has answered; one that does not answer fails the test that asked.</summary>
    public async Task<LanguageServiceSupervisor> StartedSupervisorAsync(ServiceProcessWatch watch, CancellationToken cancellationToken)
    {
        var supervisor = SupervisorFor(LanguageServiceInterpreter.Path, watch);
        await StartedAsync(supervisor, cancellationToken);
        return supervisor;
    }

    /// <summary>Starts the supervisor's service; one that does not answer fails the test that asked, saying how the start ended.</summary>
    public static async Task StartedAsync(LanguageServiceSupervisor supervisor, CancellationToken cancellationToken)
    {
        var outcome = await supervisor.StartAsync(Timeout, cancellationToken);
        if (outcome != StartOutcome.Started)
        {
            throw new InvalidOperationException($"The language service did not become healthy: {outcome}{Ended(supervisor.ExitCode, supervisor.ExitErrorTail)}.");
        }
    }

    /// <summary>Starts the service and waits for it to answer; a service that does not answer is stopped and fails the test that asked.</summary>
    public async Task<RunningService> StartHealthyAsync(string secret, CancellationToken cancellationToken)
    {
        var service = Launcher.Start(secret) ?? throw new InvalidOperationException("The language service did not start.");
        started.Add(service);
        var outcome = await Health.UntilHealthyAsync(service, secret, Timeout, cancellationToken);
        if (outcome == StartOutcome.Started)
        {
            return service;
        }

        var ended = string.Empty;
        if (outcome == StartOutcome.ProcessExited)
        {
            var tail = service.Errors is null ? null : await service.Errors.TextAsync(TimeSpan.FromSeconds(2));
            ended = Ended(service.ExitCode, tail);
        }

        throw new InvalidOperationException($"The language service did not become healthy: {outcome}{ended}.");
    }

    /// <summary>How a process that ended before answering is told in the message of the test it failed: its exit code and the end of its standard error, which is why it left.</summary>
    public static string Ended(int? exitCode, string? errorTail)
    {
        if (exitCode is null)
        {
            return string.Empty;
        }

        var written = string.IsNullOrWhiteSpace(errorTail) ? "none" : errorTail.Trim();
        return $" (exit code {exitCode}; end of its standard error: {written})";
    }

    /// <summary>How the service listening on the port answers the health route given the secret, over a connection of its own, which the service must first prove to be its holder.</summary>
    public static async Task<HttpStatusCode> HealthAsync(int port, string secret, CancellationToken cancellationToken)
    {
        using var client = LoopbackHttpClient.Create();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"http://127.0.0.1:{port}/health"));
        request.AddSecret(secret);
        using var response = await client.SendAsync(request, cancellationToken);
        return response.StatusCode;
    }

    public void Dispose()
    {
        started.ForEach(item => item.Dispose());
        Client.Dispose();
    }
}
