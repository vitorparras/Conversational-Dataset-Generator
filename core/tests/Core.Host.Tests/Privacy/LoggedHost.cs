using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Core.Host.Tests.Supervision;

namespace Core.Host.Tests.Privacy;

/// <summary>The built host with its log also in a file of its own, the moment the run began taken before it started, and a marker only this run seeds.</summary>
internal sealed partial class LoggedHost : IDisposable
{
    private const string Route = "/analysis";
    private static readonly TimeSpan RefusalLimit = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private readonly TemporaryDirectory directory;
    private readonly SweepMarker beginning;
    private readonly HostProcess host;
    private readonly string logFile;

    private LoggedHost(TemporaryDirectory directory, SweepMarker beginning, HostProcess host, string logFile)
    {
        this.directory = directory;
        this.beginning = beginning;
        this.host = host;
        this.logFile = logFile;
    }

    public string Marker { get; } = LogMarker.Fresh();

    /// <summary>What the log file holds so far.</summary>
    public string LogText => LogSweep.ReadShared(logFile);

    /// <summary>The host with the fake recognizer, or with the real language service, which it starts; the real one fails the test when it cannot run.</summary>
    public static async Task<LoggedHost> StartAsync(bool realService, CancellationToken cancellationToken)
    {
        if (realService)
        {
            _ = RealServiceGuard.Require(LanguageServiceInterpreter.Available);
        }

        var directory = new TemporaryDirectory();
        var beginning = new SweepMarker();
        var logFile = Path.Combine(directory.Path, "ZQX-LOG-0028.log");
        var address = HostProcess.FreeAddress();
        var installation = HostInstallation.Built(address).WithLogFile(logFile) with { UsesRealService = realService };
        HostProcess host;
        try
        {
            host = await HostProcess.StartAsync(installation, address, HostProcess.StartupTimeout, cancellationToken);
        }
        catch
        {
            beginning.Dispose();
            directory.Dispose();
            throw;
        }

        return new LoggedHost(directory, beginning, host, logFile);
    }

    /// <summary>Asks the host to analyse the text, naming the analysis it resumes when there is one.</summary>
    public async Task<AnalysisReply> AskAsync(string text, string? resumes, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(new Dictionary<string, string?> { ["text"] = text, ["language"] = "pt", ["resumes"] = resumes });
        using var response = await host.PostAsync(Route, body, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        return new AnalysisReply(
            response.StatusCode,
            root.TryGetProperty("reference", out var reference) ? reference.GetString() : null,
            root.TryGetProperty("occurrences", out var occurrences) ? occurrences.GetInt32() : null);
    }

    /// <summary>Posts a body the route does not know and answers with the status.</summary>
    public async Task<HttpStatusCode> RejectedAsync(string text, CancellationToken cancellationToken)
    {
        using var response = await host.PostAsync(Route, JsonSerializer.Serialize(new { text, language = "pt", zqx = 1 }), cancellationToken);
        return response.StatusCode;
    }

    /// <summary>Kills the language service by the process number the host logged, from outside, and asks until the analysis is refused.</summary>
    public async Task<AnalysisReply> AskOnceTheServiceIsDeadAsync(CancellationToken cancellationToken)
    {
        using (var service = Process.GetProcessById(ServiceProcessId()))
        {
            service.Kill(entireProcessTree: true);
            await service.WaitForExitAsync(cancellationToken);
        }

        var deadline = DateTimeOffset.UtcNow + RefusalLimit;
        var reply = await AskAsync(Marker, resumes: null, cancellationToken);
        while (reply.Status != HttpStatusCode.ServiceUnavailable && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(PollInterval, cancellationToken);
            reply = await AskAsync(Marker, resumes: null, cancellationToken);
        }

        return reply;
    }

    /// <summary>Ends the host and everything it started, and hands over what the run left to be read.</summary>
    public async Task<LogEvidence> StopAsync()
    {
        var streams = await host.StopWithErrorAsync();
        return new LogEvidence(Marker, streams.Output, streams.Error, logFile, beginning.Since);
    }

    public void Dispose()
    {
        host.Dispose();
        beginning.Dispose();
        directory.Dispose();
    }

    [GeneratedRegex(@"Serviço de linguagem iniciado \(processo (\d+),")]
    private static partial Regex Started();

    private int ServiceProcessId() =>
        int.Parse(Started().Match(LogText).Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
}
