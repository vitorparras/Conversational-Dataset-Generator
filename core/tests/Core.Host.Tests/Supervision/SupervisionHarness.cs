using Core.Host.Supervision;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Host.Tests.Supervision;

/// <summary>The pieces the supervision is built from, wired the way the host wires them; whatever a test starts through it is stopped on dispose.</summary>
internal sealed class SupervisionHarness : IDisposable
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient client = NewClient();
    private readonly List<IDisposable> started = [];

    public HealthWait Health => new(client);

    public static ServiceProcessLauncher Launcher => new(LanguageServiceInterpreter.Path);

    /// <summary>A secret no file on the machine can hold before the test makes it.</summary>
    public static string NewSecret() => $"ZQX-MARKER-7731-{Guid.NewGuid():N}";

    public LanguageServiceSupervisor SupervisorFor(string executable)
    {
        var supervisor = new LanguageServiceSupervisor(
            new ServiceProcessLauncher(executable), Health, NullLogger<LanguageServiceSupervisor>.Instance);
        started.Add(supervisor);
        return supervisor;
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

    public void Dispose()
    {
        started.ForEach(item => item.Dispose());
        client.Dispose();
    }

    private static HttpClient NewClient()
    {
        SocketsHttpHandler? handler = null;
        try
        {
            handler = new SocketsHttpHandler { UseProxy = false };
            var created = new HttpClient(handler);
            handler = null;
            return created;
        }
        finally
        {
            handler?.Dispose();
        }
    }
}
