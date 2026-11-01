using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Core.Host.Tests;
using Core.Host.Tests.Privacy;
using Core.Host.Tests.Supervision;
using Core.Tests.Integration.Hosts;

namespace Core.Tests.Integration.Privacy;

/// <summary>The built host, entered, with its log also in a file of its own, the moment the run began taken before it started, and a marker only this run seeds.</summary>
internal sealed partial class LoggedHost : IAsyncDisposable
{
    private const string Route = "/analysis";
    private static readonly TimeSpan RefusalLimit = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private readonly SweepMarker beginning;
    private readonly AuthenticatedHost host;

    private LoggedHost(SweepMarker beginning, AuthenticatedHost host)
    {
        this.beginning = beginning;
        this.host = host;
    }

    public string Marker { get; } = LogMarker.Fresh();

    /// <summary>What the log file holds so far.</summary>
    public string LogText => host.LogText;

    /// <summary>The host with the fake recognizer, or with the real language service, which it starts; the real one fails the test when it cannot run.</summary>
    public static async Task<LoggedHost> StartAsync(bool realService, CancellationToken cancellationToken)
    {
        if (realService)
        {
            _ = RealServiceGuard.Require(LanguageServiceInterpreter.Available);
        }

        var beginning = new SweepMarker();
        try
        {
            var host = await AuthenticatedHost.StartAsync(new HostSetup { RealService = realService, WithLogFile = true }, cancellationToken);
            return new LoggedHost(beginning, host);
        }
        catch
        {
            beginning.Dispose();
            throw;
        }
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
        var (stdout, stderr) = await host.StopAsync();
        return new LogEvidence(Marker, stdout, stderr, host.LogFile, beginning.Since);
    }

    public async ValueTask DisposeAsync()
    {
        await host.DisposeAsync();
        beginning.Dispose();
    }

    [GeneratedRegex(@"Serviço de linguagem iniciado \(processo (\d+),")]
    private static partial Regex Started();

    private int ServiceProcessId() =>
        int.Parse(Started().Match(LogText).Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
}
