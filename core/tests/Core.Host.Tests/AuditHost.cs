using System.Globalization;
using Core.Host.Endpoints;
using Core.Persistence.ServerGuarantees;
using Core.Platform.Configuration;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests;

/// <summary>The host composition with the session gate and the audit route, serving in process over a folder that stands for the server's log.</summary>
internal sealed class AuditHost : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly HttpClient client;
    private readonly string directory;
    private readonly RecordedLog recorded;

    private AuditHost(WebApplication app, Uri address, string directory, string session, RecordedLog recorded)
    {
        this.app = app;
        this.directory = directory;
        this.recorded = recorded;
        client = new HttpClient { BaseAddress = address };
        Session = session;
    }

    /// <summary>What the host wrote to its log while it served, each line with its level.</summary>
    public IReadOnlyList<string> Logged => recorded.Lines;

    /// <summary>The identifier of a live session, as the cookie carries it.</summary>
    public string Session { get; }

    public static async Task<AuditHost> StartAsync(CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"cdg-audit-host-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        var address = HostProcess.FreePlainAddress();
        var builder = CompositionRoot.ClosedBuilder(address.GetLeftPart(UriPartial.Authority));
        CompositionRoot.Compose(builder, ClosedConfiguration.Load(new ConfigurationSchema([HostConfiguration.Section])).View, fileLog: null);
        var sessions = new SessionStore(new SessionLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8)), TimeProvider.System);
        var recorded = new RecordedLog();
        _ = builder.Logging.AddProvider(recorded);
        _ = builder.Services.AddSingleton(sessions).AddSingleton(new ServerAuditLog(directory));
        var app = builder.Build();
        _ = app.Use(async (context, next) =>
        {
            context.Request.Scheme = context.Request.Headers.ContainsKey(SessionedHost.EncryptedMarker) ? "https" : context.Request.Scheme;
            await next(context);
        });
        SessionGate.Use(app);
        AuditEndpoint.Map(app);
        await app.StartAsync(cancellationToken);
        return new AuditHost(app, address, directory, sessions.Open(personId: 1), recorded);
    }

    /// <summary>Writes a log file with the audit lines the server would write for the commands, one second apart from the instant.</summary>
    public void WriteLog(DateTimeOffset first, params string[] commands) =>
        File.WriteAllLines(Path.Combine(directory, "postgresql-a.csv"), commands.Select((command, index) => AuditLine(first.AddSeconds(index), command)));

    public void WriteLog(params string[] lines) => File.WriteAllLines(Path.Combine(directory, "postgresql-a.csv"), lines);

    /// <summary>
    /// Writes a closed file whose last line the server's fall cut short, after one whole line, and a newest file with one more whole line: the cut line is the
    /// start of a line with no line break after it.
    /// </summary>
    public void WriteCutLog(DateTimeOffset first)
    {
        var cut = AuditLine(first.AddSeconds(1), "UPDATE")[..40];
        File.WriteAllText(Path.Combine(directory, "postgresql-a.csv"), AuditLine(first, "INSERT") + "\n" + cut);
        File.WriteAllLines(Path.Combine(directory, "postgresql-b.csv"), [AuditLine(first.AddSeconds(2), "DELETE")]);
    }

    /// <summary>One csvlog line of an audited statement, with a marker in the statement that no answer may carry.</summary>
    public static string AuditLine(DateTimeOffset at, string command, string zone = "UTC")
    {
        var t = at.UtcDateTime;
        var time = string.Create(CultureInfo.InvariantCulture, $"{t.Year:D4}-{t.Month:D2}-{t.Day:D2} {t.Hour:D2}:{t.Minute:D2}:{t.Second:D2}.{t.Millisecond:D3}");
        var message = $"AUDIT: SESSION,1,1,WRITE,{command},TABLE,public.person,/* zqx-text */ {command} public.person,<not logged>";
        return $"{time} {zone},\"app\",\"cdg\",90,\"127.0.0.1:1\",6ae775a3.5a,1,\"{command}\",{time} {zone},3/2,750,LOG,00000,\"{message}\",,,,,,,,,\"\",\"client backend\",,0";
    }

    public async Task<HttpResponseMessage> GetAsync(string query, bool withSession, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(AuditEndpoint.Route + query, UriKind.Relative));
        if (withSession)
        {
            request.Headers.Add("Cookie", $"{SessionCookie.Name}={Session}");
            request.Headers.Add(SessionedHost.EncryptedMarker, "1");
        }

        return await client.SendAsync(request, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        await app.DisposeAsync();
        Directory.Delete(directory, recursive: true);
    }
}
