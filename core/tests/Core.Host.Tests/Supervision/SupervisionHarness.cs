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

    public static ServiceProcessLauncher Launcher => new(LanguageServiceInterpreter.Path);

    /// <summary>A secret no file on the machine can hold before the test makes it.</summary>
    public static string NewSecret() => $"ZQX-MARKER-7731-{Guid.NewGuid():N}";

    public LanguageServiceSupervisor SupervisorFor(string executable) => SupervisorFor(executable, new ServiceProcessWatch(), Log);

    public LanguageServiceSupervisor SupervisorFor(string executable, ServiceProcessWatch watch) => SupervisorFor(executable, watch, Log);

    /// <summary>A supervisor that writes its lines to the logger given, so a test can see when each was written.</summary>
    public LanguageServiceSupervisor SupervisorFor(string executable, ILogger<LanguageServiceSupervisor> logger) =>
        SupervisorFor(executable, new ServiceProcessWatch(), logger);

    public LanguageServiceSupervisor SupervisorFor(string executable, ServiceProcessWatch watch, ILogger<LanguageServiceSupervisor> logger)
    {
        var supervisor = new LanguageServiceSupervisor(new ServiceProcessLauncher(executable), Health, watch, logger);
        started.Add(supervisor);
        return supervisor;
    }

    /// <summary>A supervisor whose service has answered; one that does not answer fails the test that asked.</summary>
    public async Task<LanguageServiceSupervisor> StartedSupervisorAsync(ServiceProcessWatch watch, CancellationToken cancellationToken)
    {
        var supervisor = SupervisorFor(LanguageServiceInterpreter.Path, watch);
        var outcome = await supervisor.StartAsync(Timeout, cancellationToken);
        return outcome == StartOutcome.Started
            ? supervisor
            : throw new InvalidOperationException($"The language service did not become healthy: {outcome}.");
    }

    /// <summary>Starts the service and waits for it to answer; a service that does not answer is stopped and fails the test that asked.</summary>
    public async Task<RunningService> StartHealthyAsync(string secret, CancellationToken cancellationToken)
    {
        var service = Launcher.Start(secret) ?? throw new InvalidOperationException("The language service did not start.");
        started.Add(service);
        var outcome = await Health.UntilHealthyAsync(service, secret, Timeout, cancellationToken);
        return outcome == StartOutcome.Started
            ? service
            : throw new InvalidOperationException($"The language service did not become healthy: {outcome}.");
    }

    /// <summary>How the service listening on the port answers the health route given the secret.</summary>
    public async Task<HttpStatusCode> HealthAsync(int port, string secret, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"http://127.0.0.1:{port}/health"));
        request.Headers.Add("x-service-secret", secret);
        using var response = await Client.SendAsync(request, cancellationToken);
        return response.StatusCode;
    }

    public void Dispose()
    {
        started.ForEach(item => item.Dispose());
        Client.Dispose();
    }
}
