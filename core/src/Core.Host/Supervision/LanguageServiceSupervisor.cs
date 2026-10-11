using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Platform.Startup;

namespace Core.Host.Supervision;

/// <summary>Whether the language service can be called now, and if not, why.</summary>
/// <param name="IsAvailable">Whether the process is alive and was healthy when it started.</param>
/// <param name="Reason">Why not, in words that carry no path and no content; empty when available.</param>
internal readonly record struct ServiceAvailability(bool IsAvailable, string Reason);

/// <summary>
/// Starts the language service as a child process, hands it the secret of this start on its
/// standard input, waits until it answers an authenticated request, and says whether it is still
/// there (issue 29 CA-3 and CA-5, issue 32 CA-4).
/// </summary>
/// <remarks>
/// The secret is 32 random bytes, written as one line and followed by the closing of the stream.
/// It is never an argument, a file, an environment variable or a log value. A restart is asked for,
/// never automatic, and makes a new secret and a new port: the old secret stops being accepted.
/// </remarks>
internal sealed class LanguageServiceSupervisor : IStartupCheck, IAsyncDisposable
{
    /// <summary>The header the service reads the secret from.</summary>
    public const string SecretHeader = "X-Service-Secret";

    private const int SecretBytes = 32;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StopWait = TimeSpan.FromSeconds(10);

    private readonly LanguageServiceOptions options;
    private readonly ILogger logger;
    private readonly SocketsHttpHandler handler = new()
    {
        // The text of a material never goes through a proxy, whatever the environment says.
        UseProxy = false,
        ConnectTimeout = TimeSpan.FromSeconds(1),
    };

    private readonly HttpClient client;
    private readonly Lock gate = new();

    private Process? process;
    private string? secret;
    private int port;
    private string state = "o serviço ainda não foi iniciado";
    private bool stopping;
    private bool healthy;
    private bool disposed;

    /// <summary>Creates a supervisor that has not started anything.</summary>
    public LanguageServiceSupervisor(LanguageServiceOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        this.options = options;
        this.logger = logger;
        client = new HttpClient(handler, disposeHandler: false) { Timeout = AttemptTimeout };
    }

    /// <inheritdoc />
    public string Name => "LanguageService";

    /// <summary>The version of the model the service reported when it started; null when it has none.</summary>
    public string? ModelVersion { get; private set; }

    /// <summary>The identifier of the child process, for the log and for tests; zero before the first start.</summary>
    public int ProcessId { get; private set; }

    /// <summary>The start information of the last start, which tests read to see what the child was given.</summary>
    public ProcessStartInfo? LastStartInfo { get; private set; }

    /// <summary>Whether the service can be called now. A dead process is unavailable, whatever it was before.</summary>
    public ServiceAvailability Availability
    {
        get
        {
            lock (gate)
            {
                return process is { HasExited: false } && !stopping && secret is not null && healthy
                    ? new ServiceAvailability(true, string.Empty)
                    : new ServiceAvailability(false, state);
            }
        }
    }

    /// <summary>Starts the service and waits for it to be healthy; this is the startup check.</summary>
    public async Task<CheckResult> RunAsync(CancellationToken cancellationToken) =>
        await StartAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Builds a request to the service with the secret of the current start. Throws when the service
    /// is not available, so a caller cannot send text to a service that is not there.
    /// </summary>
    public HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        lock (gate)
        {
            var availability = Availability;
            if (!availability.IsAvailable)
            {
                throw new InvalidOperationException("serviço de linguagem indisponível: " + availability.Reason);
            }

            return Build(method, path, secret!, port);
        }
    }

    /// <summary>The live result of the check: passed only while the process is alive.</summary>
    public CheckResult Current()
    {
        var availability = Availability;
        return availability.IsAvailable
            ? CheckResult.Pass(Name, string.Create(CultureInfo.InvariantCulture, $"respondendo em 127.0.0.1:{port}"), ExpectedSource)
            : CheckResult.Fail(Name, availability.Reason, ExpectedSource);
    }

    /// <summary>The startup report with this check's entry replaced by its live result.</summary>
    public StartupReport Refresh(StartupReport startup)
    {
        ArgumentNullException.ThrowIfNull(startup);
        var live = Current();
        return new StartupReport([.. startup.Results.Select(result => result.Name == Name ? live : result)]);
    }

    /// <summary>Stops the service if it is running and starts it again with a new secret.</summary>
    public async Task<CheckResult> RestartAsync(CancellationToken cancellationToken)
    {
        logger.ServiceRestartRequested();
        await StopAsync().ConfigureAwait(false);
        return await StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Ends the child process and everything it started.</summary>
    /// <remarks>
    /// There is no gentler way to ask a child that has no console of its own: the service keeps no
    /// state, and each log line is flushed when written, so ending it loses nothing.
    /// </remarks>
    public async Task StopAsync()
    {
        Process? target;
        lock (gate)
        {
            target = process;
            if (target is null)
            {
                return;
            }

            stopping = true;
            state = "o serviço foi encerrado pelo núcleo";
        }

        var id = ProcessId;
        try
        {
            if (!target.HasExited)
            {
                target.Kill(entireProcessTree: true);
            }

            using var wait = new CancellationTokenSource(StopWait);
            await target.WaitForExitAsync(wait.Token).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // It exited between the check and the kill.
        }
        catch (OperationCanceledException)
        {
            // It did not end in time; the system reclaims it when the core ends.
        }
        finally
        {
            lock (gate)
            {
                target.Dispose();
                process = null;
                secret = null;
            }

            logger.ServiceStopped(id);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await StopAsync().ConfigureAwait(false);
        client.Dispose();
        handler.Dispose();
    }

    /// <summary>A fresh secret: 32 random bytes as 64 lowercase hexadecimal characters.</summary>
    internal static string NewSecret() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(SecretBytes));

    private static string ExpectedSource =>
        "o comando declarado em LanguageService:Command, que sobe o serviço de linguagem local";

    private static HttpRequestMessage Build(HttpMethod method, string path, string secret, int port)
    {
        var request = new HttpRequestMessage(
            method,
            new Uri(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}{path}")));
        request.Headers.Add(SecretHeader, secret);
        return request;
    }

    private static int FreeLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var free = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return free;
    }

    private async Task<CheckResult> StartAsync(CancellationToken cancellationToken)
    {
        string newSecret;
        int newPort;
        Process child;
        lock (gate)
        {
            if (process is not null)
            {
                throw new InvalidOperationException("the language service is already running");
            }

            newSecret = NewSecret();
            newPort = FreeLoopbackPort();
            stopping = false;
            healthy = false;
            ModelVersion = null;
            child = new Process { StartInfo = StartInfoFor(newPort), EnableRaisingEvents = true };
            LastStartInfo = child.StartInfo;
        }

        try
        {
            _ = child.Start();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            child.Dispose();
            return Fail("o comando declarado em LanguageService:Command não pôde ser iniciado");
        }

        lock (gate)
        {
            process = child;
            secret = newSecret;
            port = newPort;
            ProcessId = child.Id;
            state = "o serviço ainda não respondeu";
        }

        child.Exited += OnExited;
        logger.ServiceStarted(child.Id, newPort);

        // One line, then the stream is closed: the service reads once and the secret has no other path.
        try
        {
            await child.StandardInput.WriteAsync((newSecret + "\n").AsMemory(), cancellationToken).ConfigureAwait(false);
            child.StandardInput.Close();
        }
        catch (IOException)
        {
            // The child is already gone; the wait below reports its exit code.
        }

        return await WaitUntilHealthyAsync(child, newSecret, newPort, cancellationToken).ConfigureAwait(false);
    }

    private ProcessStartInfo StartInfoFor(int servicePort)
    {
        var info = new ProcessStartInfo
        {
            FileName = options.Command,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        foreach (var argument in options.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        info.ArgumentList.Add("--port");
        info.ArgumentList.Add(servicePort.ToString(CultureInfo.InvariantCulture));
        return info;
    }

    private async Task<CheckResult> WaitUntilHealthyAsync(
        Process child, string healthSecret, int healthPort, CancellationToken cancellationToken)
    {
        var waited = Stopwatch.StartNew();
        while (waited.Elapsed < options.StartupTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (child.HasExited)
            {
                return Fail(string.Create(
                    CultureInfo.InvariantCulture,
                    $"o processo encerrou com código {child.ExitCode} antes de responder"));
            }

            var outcome = await ProbeAsync(healthSecret, healthPort, cancellationToken).ConfigureAwait(false);
            switch (outcome)
            {
                case Probe.Healthy:
                    lock (gate)
                    {
                        healthy = true;
                        state = "o serviço respondeu";
                    }

                    logger.ServiceHealthy();
                    return Current();
                case Probe.SecretRefused:
                    await StopAsync().ConfigureAwait(false);
                    return Fail("o serviço recusou o segredo desta partida");
                case Probe.NotYet:
                    break;
                default:
                    throw new InvalidOperationException("unknown probe outcome");
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }

        await StopAsync().ConfigureAwait(false);
        return Fail(string.Create(
            CultureInfo.InvariantCulture,
            $"o serviço não respondeu em {options.StartupTimeout.TotalSeconds:F0} s (LanguageService:StartupTimeoutSeconds)"));
    }

    private async Task<Probe> ProbeAsync(string probeSecret, int probePort, CancellationToken cancellationToken)
    {
        using var request = Build(HttpMethod.Get, "/health", probeSecret, probePort);
        try
        {
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return Probe.SecretRefused;
            }

            if (!response.IsSuccessStatusCode)
            {
                return Probe.NotYet;
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var answer = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (answer.RootElement.TryGetProperty("model_version", out var model) && model.ValueKind == JsonValueKind.String)
            {
                ModelVersion = model.GetString();
            }

            return Probe.Healthy;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or OperationCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            // Not listening yet, or the answer was not the service's: ask again until the timeout.
            return Probe.NotYet;
        }
    }

    private CheckResult Fail(string reason)
    {
        lock (gate)
        {
            state = reason;
        }

        logger.ServiceNotStarted(reason);
        return CheckResult.Fail(Name, reason, ExpectedSource);
    }

    private void OnExited(object? sender, EventArgs e)
    {
        if (sender is not Process exited)
        {
            return;
        }

        lock (gate)
        {
            if (stopping || process != exited)
            {
                return;
            }

            int code;
            try
            {
                code = exited.ExitCode;
            }
            catch (InvalidOperationException)
            {
                return;
            }

            state = string.Create(CultureInfo.InvariantCulture, $"o processo encerrou com código {code}");
            logger.ServiceExited(ProcessId, code);
        }
    }

    private enum Probe
    {
        NotYet = 0,
        Healthy = 1,
        SecretRefused = 2,
    }
}
