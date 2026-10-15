using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Core.Host.Tests;

/// <summary>The built host, started as its own process on a free loopback port and killed on dispose.</summary>
internal sealed class HostProcess : IDisposable
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private readonly Process process;
    private readonly HttpClient client;

    private HostProcess(Process process, Uri baseAddress)
    {
        this.process = process;
        client = new HttpClient { BaseAddress = baseAddress };
    }

    /// <summary>The address the host was told to listen on, as a command-line argument.</summary>
    public Uri BaseAddress => client.BaseAddress!;

    public static async Task<HostProcess> StartAsync(CancellationToken cancellationToken)
    {
        var baseAddress = new Uri($"http://127.0.0.1:{FreePort()}");
        var process = Start(StartInfo(baseAddress, AppContext.BaseDirectory));
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var host = new HostProcess(process, baseAddress);
        await host.WaitUntilListeningAsync(cancellationToken);
        return host;
    }

    /// <summary>Runs the host from the directory until it exits by itself, and hands over the exit code and what it wrote to the error stream.</summary>
    public static async Task<HostExit> RunToExitAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        using var process = Start(StartInfo(new Uri($"http://127.0.0.1:{FreePort()}"), workingDirectory));
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).WaitAsync(StartupTimeout, cancellationToken);
        _ = await output;
        return new HostExit(process.ExitCode, await error);
    }

    public Task<HttpResponseMessage> GetAsync(string route, CancellationToken cancellationToken) =>
        client.GetAsync(new Uri(route, UriKind.Relative), cancellationToken);

    public void Dispose()
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }

        process.Dispose();
        client.Dispose();
    }

    private static Process Start(ProcessStartInfo info) =>
        Process.Start(info) ?? throw new InvalidOperationException("The host process did not start.");

    private static ProcessStartInfo StartInfo(Uri baseAddress, string workingDirectory)
    {
        var fileName = OperatingSystem.IsWindows() ? "Core.Host.exe" : "Core.Host";
        var info = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, fileName))
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add($"--Host:Urls={baseAddress.GetLeftPart(UriPartial.Authority)}");
        return info;
    }

    /// <summary>A port the system hands out and releases at once; the host binds it a moment later.</summary>
    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private async Task WaitUntilListeningAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + StartupTimeout;
        while (DateTimeOffset.UtcNow < deadline && !process.HasExited)
        {
            if (await AnswersAsync(cancellationToken))
            {
                return;
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        throw new TimeoutException($"The host did not answer within {StartupTimeout}; exited: {process.HasExited}.");
    }

    private async Task<bool> AnswersAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(new Uri("/status", UriKind.Relative), cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}
