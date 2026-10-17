using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Core.Host.Tests;

/// <summary>The built host as its own process: started, awaited or killed, and never left behind on dispose.</summary>
internal sealed class HostProcess : IDisposable
{
    public static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(5);

    private readonly Process process;
    private readonly HttpClient client;
    private readonly Task<string> error;
    private readonly Task<string> output;

    private HostProcess(Process process, Uri baseAddress)
    {
        this.process = process;
        client = new HttpClient { BaseAddress = baseAddress };
        error = process.StandardError.ReadToEndAsync();
        output = process.StandardOutput.ReadToEndAsync();
    }

    /// <summary>The address the host was told to listen on.</summary>
    public Uri BaseAddress => client.BaseAddress!;

    public bool HasExited => process.HasExited;

    /// <summary>The built host, listening on a free loopback port.</summary>
    public static Task<HostProcess> StartAsync(CancellationToken cancellationToken)
    {
        var baseAddress = FreeAddress();
        return StartAsync(HostInstallation.Built(baseAddress), baseAddress, StartupTimeout, cancellationToken);
    }

    /// <summary>Starts the installation and waits until it answers on the address; a host that never answers is killed before the failure propagates.</summary>
    public static async Task<HostProcess> StartAsync(
        HostInstallation installation, Uri baseAddress, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var host = Launch(installation, baseAddress);
        try
        {
            await host.WaitUntilListeningAsync(timeout, cancellationToken);
            var listening = host;
            host = null;
            return listening;
        }
        finally
        {
            host?.Dispose();
        }
    }

    /// <summary>Runs the installation until it exits by itself and hands over the exit code and the error stream.</summary>
    public static async Task<HostExit> RunToExitAsync(
        HostInstallation installation, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var host = Launch(installation, FreeAddress());
        return await host.WaitForExitAsync(timeout, cancellationToken);
    }

    /// <summary>Starts the process without waiting for anything.</summary>
    public static HostProcess Launch(HostInstallation installation, Uri baseAddress) =>
        new(Process.Start(StartInfo(installation)) ?? throw new InvalidOperationException("The host process did not start."), baseAddress);

    /// <summary>An address on a port the system hands out and releases at once.</summary>
    public static Uri FreeAddress()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}");
    }

    /// <summary>Waits for the process to end by itself; past the timeout it is killed, its streams drained, and the wait fails.</summary>
    public async Task<HostExit> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var exited = process.WaitForExitAsync(cancellationToken);
        if (await Task.WhenAny(exited, Task.Delay(timeout, cancellationToken)) != exited)
        {
            Kill();
            await DrainAsync();
            throw new TimeoutException($"The host did not exit within {timeout}; it was killed.");
        }

        await exited;
        await DrainAsync();
        return new HostExit(process.ExitCode, await error);
    }

    /// <summary>Kills the host and hands over everything it wrote to its output stream until then.</summary>
    public async Task<string> StopAsync()
    {
        Kill();
        return await output;
    }

    public Task<HttpResponseMessage> GetAsync(string route, CancellationToken cancellationToken) =>
        client.GetAsync(new Uri(route, UriKind.Relative), cancellationToken);

    public void Dispose()
    {
        Kill();
        _ = Task.WaitAll([error, output], DrainTimeout);
        process.Dispose();
        client.Dispose();
    }

    private static ProcessStartInfo StartInfo(HostInstallation installation)
    {
        var info = new ProcessStartInfo(installation.ExecutablePath)
        {
            WorkingDirectory = installation.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var variable in installation.Environment)
        {
            info.Environment[variable.Key] = variable.Value;
        }

        foreach (var argument in installation.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        return info;
    }

    private void Kill()
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }
    }

    private async Task DrainAsync()
    {
        _ = await output;
        _ = await error;
    }

    private async Task WaitUntilListeningAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline && !process.HasExited)
        {
            if (await AnswersAsync(cancellationToken))
            {
                return;
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        throw new TimeoutException($"The host did not answer within {timeout}; exited: {process.HasExited}.");
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
