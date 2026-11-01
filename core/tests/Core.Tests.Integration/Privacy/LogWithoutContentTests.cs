using Core.Persistence.Tests.Fixtures;
using Core.Host.Tests.Privacy;
using System.Net;

namespace Core.Tests.Integration.Privacy;

/// <summary>
/// The text a person gave to be analysed reaches no destination of the log on the normal path: not the console, not the
/// error stream the language service shares with the host, not the log file, not any file the run wrote
/// (issue #251, CA-1; requirement 021). Each test also shows the flow ran, so a sweep over nothing cannot pass.
/// </summary>
[Trait("Category", "Database")]
public sealed class LogWithoutContentTests
{
    private const string Listening = "Now listening on: ";

    [DatabaseFact]
    public async Task OccurrencesFoundByTheFakeLeaveTheMarkerInNoDestination()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await LoggedHost.StartAsync(realService: false, cancellationToken);

        var reply = await host.AskAsync($"ZQX-FAKE-PERSON falou com ZQX-FAKE-PERSON {host.Marker}", resumes: null, cancellationToken);
        var evidence = await host.StopAsync();

        Assert.Equal((HttpStatusCode.OK, 2), (reply.Status, reply.Occurrences));
        Assert.Contains(Listening, evidence.Stdout, StringComparison.Ordinal);
        Assert.Contains(Listening, host.LogText, StringComparison.Ordinal);
        Assert.Empty(LogSweep.Leaks(evidence));
    }

    [Trait("Category", "RealService")]
    [DatabaseFact]
    public async Task TextCrossingTheBoundaryToTheRealServiceLeavesTheMarkerInNoDestination()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await LoggedHost.StartAsync(realService: true, cancellationToken);

        var reply = await host.AskAsync($"falei com PESSOA_1 ontem {host.Marker}", resumes: null, cancellationToken);
        var evidence = await host.StopAsync();

        Assert.Equal(HttpStatusCode.OK, reply.Status);
        Assert.Contains("Pedido analisado pelo serviço de linguagem", host.LogText, StringComparison.Ordinal);
        Assert.Contains("Pedido analisado pelo serviço de linguagem", evidence.Stdout, StringComparison.Ordinal);
        Assert.Contains("\"event\": \"analyzed\"", evidence.Stderr, StringComparison.Ordinal);
        Assert.Empty(LogSweep.Leaks(evidence));
    }
}
