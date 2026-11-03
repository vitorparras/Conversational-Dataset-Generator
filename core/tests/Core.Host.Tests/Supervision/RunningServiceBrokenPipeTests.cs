using System.Diagnostics;
using System.Text;
using Core.Host.Supervision;

namespace Core.Host.Tests.Supervision;

/// <summary>
/// A service that ends before the secret is delivered (#562): the write to its standard input fails and the pipe is marked broken, and closing the input afterwards flushes it again and
/// raises "Pipe is broken". The process here is one that has already ended when the secret is written, so the order is the same every time and not a race.
/// </summary>
public sealed class RunningServiceBrokenPipeTests
{
    [Fact]
    public async Task ServiceThatEndedBeforeTheDeliveryOfTheSecretIsReleasedWithoutAnError()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var process = Process.Start(new ProcessStartInfo("dotnet", "--version")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            UseShellExecute = false,
        })!;
        await process.WaitForExitAsync(cancellationToken);
        ServiceProcessLauncher.Deliver(process, SupervisionHarness.NewSecret());

        var released = Record.Exception(() =>
        {
            using var service = new RunningService(process, port: 1);
        });

        Assert.Null(released);
    }
}
