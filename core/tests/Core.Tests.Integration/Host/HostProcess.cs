using System.Diagnostics;
using System.Text;
using Core.Tests.Shared;

namespace Core.Tests.Integration.Host;

/// <summary>Runs the real Core.Host executable as a child process.</summary>
internal sealed class HostProcess : IAsyncDisposable
{
    private readonly Process process;
    private readonly StringBuilder output = new();
    private readonly Lock gate = new();

    private HostProcess(Process process)
    {
        this.process = process;
    }

    public int Id => process.Id;

    public bool HasExited => process.HasExited;

    public int ExitCode => process.ExitCode;

    /// <summary>Everything the process wrote to stdout and stderr so far.</summary>
    public string Output
    {
        get
        {
            lock (gate)
            {
                return output.ToString();
            }
        }
    }

    public static string HostAssemblyPath =>
        Path.Combine(
            RepositoryRoot.Find().FullName,
            "core", "src", "Core.Host", "bin", RepositoryRoot.Configuration, "net10.0", "Core.Host.dll");

    public static HostProcess Start(
        IEnumerable<string> arguments,
        IReadOnlyDictionary<string, string?>? environment = null,
        string? workingDirectory = null)
    {
        var info = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Path.GetTempPath(),
        };
        info.ArgumentList.Add(HostAssemblyPath);
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in environment ?? new Dictionary<string, string?>())
        {
            info.Environment[name] = value;
        }

        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        var host = new HostProcess(process);
        process.OutputDataReceived += (_, e) => host.Append(e.Data);
        process.ErrorDataReceived += (_, e) => host.Append(e.Data);
        _ = process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return host;
    }

    /// <summary>Waits until the output contains <paramref name="text"/> or the process exits.</summary>
    public async Task<bool> WaitForOutputAsync(string text, TimeSpan timeout)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < timeout)
        {
            if (Output.Contains(text, StringComparison.Ordinal))
            {
                return true;
            }

            if (process.HasExited)
            {
                await process.WaitForExitAsync(TestContext.Current.CancellationToken);
                return Output.Contains(text, StringComparison.Ordinal);
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        return false;
    }

    public async Task<int> WaitForExitAsync(TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        await process.WaitForExitAsync(cancellation.Token);
        return process.ExitCode;
    }

    public async ValueTask DisposeAsync()
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        process.Dispose();
    }

    private void Append(string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (gate)
        {
            _ = output.AppendLine(line);
        }
    }
}
