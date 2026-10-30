using System.Diagnostics;
using Core.Host.Supervision;

namespace Core.Host.Tests.Supervision;

/// <summary>A start that ends without an answer says how far it got: whether the service ever announced a port, what it wrote first, and whether it was still there.</summary>
[Trait("Category", "RealService")]
public sealed class RunningServiceReachedTests
{
    private const string Silent = "import sys; sys.stdin.read()";
    private const string WritesSomethingElse = "import sys; print('not the announcement', flush=True); sys.stdin.read()";
    private const string Ends = "pass";
    private const int AnnouncedPort = 4321;

    [Fact]
    public void ServiceThatWroteNothingYetDidNotAnnounceThePort()
    {
        using var service = RunningService.Announcing(Start(Silent));

        Assert.Equal("o serviço iniciou e não anunciou a porta", service.Reached);
    }

    [Fact]
    public async Task ServiceWhoseFirstLineIsNotTheAnnouncementSaysSo()
    {
        using var service = RunningService.Announcing(Start(WritesSomethingElse));
        var announced = await service.PortAnnouncedAsync().WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);

        Assert.False(announced);
        Assert.Equal("o que o serviço escreveu primeiro não é o anúncio da porta", service.Reached);
    }

    [Fact]
    public async Task ServiceThatEndedBeforeAnnouncingThePortSaysSo()
    {
        var process = Start(Ends);
        using var service = RunningService.Announcing(process);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.Equal("o serviço saiu antes de anunciar a porta", service.Reached);
    }

    [Fact]
    public void ServiceThatAnnouncedThePortAndDoesNotAnswerNamesThePort()
    {
        using var service = new RunningService(Start(Silent), AnnouncedPort);

        Assert.Equal("o serviço anunciou a porta 4321 e a saúde não respondeu", service.Reached);
    }

    [Fact]
    public async Task ServiceThatEndedAfterAnnouncingThePortNamesThePort()
    {
        var process = Start(Ends);
        using var service = new RunningService(process, AnnouncedPort);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.Equal("o serviço saiu depois de anunciar a porta 4321", service.Reached);
    }

    private static Process Start(string code)
    {
        var info = new ProcessStartInfo(LanguageServiceInterpreter.Path)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add(code);
        return Process.Start(info) ?? throw new InvalidOperationException("The stand-in did not start.");
    }
}
