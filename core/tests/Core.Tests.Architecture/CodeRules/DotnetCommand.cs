using System.Diagnostics;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>A <c>dotnet</c> command run to its end under a time limit; past the limit, or on any failure to wait, the whole process tree is killed.</summary>
internal static class DotnetCommand
{
    public static readonly TimeSpan Limit = TimeSpan.FromMinutes(5);

    public static async Task<CommandOutcome> RunAsync(IReadOnlyList<string> arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        using var process = Process.Start(StartInfo(arguments, workingDirectory)) ?? throw new InvalidOperationException("The command did not start.");
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(Limit);
        try
        {
            await process.WaitForExitAsync(limit.Token);
            return new CommandOutcome(process.ExitCode, await output, await error);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"dotnet {arguments[0]} in {Path.GetFileName(workingDirectory)} did not end within {Limit}.");
        }
        finally
        {
            Kill(process);
        }
    }

    private static ProcessStartInfo StartInfo(IReadOnlyList<string> arguments, string workingDirectory)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        return info;
    }

    private static void Kill(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }
    }
}
