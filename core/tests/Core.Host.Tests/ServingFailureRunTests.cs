using Core.Platform.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Core.Host.Tests;

public sealed class ServingFailureRunTests
{
    [Fact]
    public void ExceptionOfAnyKindFromTheHostEndsTheRunWithTheServingFailedCode()
    {
        using var app = HostThatFailsToStart(new FormatException("ZQX-MATERIAL-7731"));

        var code = HostLaunch.Ready(app).Run();

        Assert.Equal(ExitCode.ServingFailed, code);
    }

    [Fact]
    public void CancellationFromTheHostIsAlsoAFailureOfTheRun()
    {
        using var app = HostThatFailsToStart(new OperationCanceledException());

        Assert.Equal(ExitCode.ServingFailed, HostLaunch.Ready(app).Run());
    }

    [Fact]
    public void FailureTheProcessCannotReportIsNotReportedAndKeepsItsOwnEnd()
    {
        using var app = HostThatFailsToStart(new InsufficientExecutionStackException());

        _ = Assert.Throws<InsufficientExecutionStackException>(() => HostLaunch.Ready(app).Run());
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
