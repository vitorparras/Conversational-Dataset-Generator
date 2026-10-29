using Core.Platform.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Core.Host.Tests;

public sealed class ServingFailureRunTests
{
    [Fact]
    public async Task ExceptionOfAnyKindFromTheHostEndsTheRunWithTheServingFailedCode()
    {
        using var app = HostThatFailsToStart(new FormatException("ZQX-MATERIAL-7731"));

        var code = await HostLaunch.Ready(app).RunAsync();

        Assert.Equal(ExitCode.ServingFailed, code);
    }

    [Fact]
    public async Task CancellationFromTheHostIsAlsoAFailureOfTheRun()
    {
        using var app = HostThatFailsToStart(new OperationCanceledException());

        Assert.Equal(ExitCode.ServingFailed, await HostLaunch.Ready(app).RunAsync());
    }

    [Fact]
    public async Task FailureTheProcessCannotReportIsNotReportedAndKeepsItsOwnEnd()
    {
        using var app = HostThatFailsToStart(new InsufficientExecutionStackException());

        _ = await Assert.ThrowsAsync<InsufficientExecutionStackException>(() => HostLaunch.Ready(app).RunAsync());
    }

    [Theory]
    [InlineData(typeof(OutOfMemoryException), false)]
    [InlineData(typeof(AccessViolationException), false)]
    [InlineData(typeof(InsufficientExecutionStackException), false)]
    [InlineData(typeof(IOException), true)]
    [InlineData(typeof(FormatException), true)]
    [InlineData(typeof(InvalidOperationException), true)]
    public void OnlyWhatIsFatalToTheProcessIsLeftOutOfTheReport(Type kind, bool reported)
    {
        var exception = (Exception)Activator.CreateInstance(kind)!;

        Assert.Equal(reported, ServingFailure.CanReport(exception));
    }

    private static WebApplication HostThatFailsToStart(Exception failure)
    {
        var builder = CompositionRoot.ClosedBuilder("http://127.0.0.1:0");
        _ = builder.Services.AddHostedService(_ => new FailingAtStart(failure));
        return builder.Build();
    }

    private sealed class FailingAtStart(Exception failure) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.FromException(failure);

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
