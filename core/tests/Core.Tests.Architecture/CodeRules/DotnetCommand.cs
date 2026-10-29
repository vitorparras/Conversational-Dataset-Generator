using System.Diagnostics;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>
/// A <c>dotnet</c> command run to its end under a time limit; past the limit, or on any failure to wait, the whole process tree is killed.
/// Only a few run at once in the test process, however many tests ask: each one brings an MSBuild and a compiler with it, and a
/// machine already busy with the other test projects cannot start that many.
/// </summary>
internal static class DotnetCommand
{
    public static readonly TimeSpan Limit = TimeSpan.FromMinutes(5);

    private const int AtOnce = 2;

    /// <summary>The exit code Windows gives a process that could not initialize (STATUS_DLL_INIT_FAILED), before its first line of code.</summary>
    private const int DllInitFailed = unchecked((int)0xC0000142);

    private static readonly SemaphoreSlim Running = new(AtOnce);

    public static async Task<CommandOutcome> RunAsync(
        IReadOnlyList<string> arguments, string workingDirectory, CancellationToken cancellationToken, IReadOnlyDictionary<string, string>? environment = null)
    {
        await Running.WaitAsync(cancellationToken);
        try
        {
            return await RunToEndAsync(arguments, workingDirectory, environment ?? new Dictionary<string, string>(), cancellationToken);
        }
        finally
        {
            _ = Running.Release();
        }
    }

    private static async Task<CommandOutcome> RunToEndAsync(
        IReadOnlyList<string> arguments, string workingDirectory, IReadOnlyDictionary<string, string> environment, CancellationToken cancellationToken)
    {
        using var process = Process.Start(StartInfo(arguments, workingDirectory, environment)) ?? throw new InvalidOperationException("The command did not start.");
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(Limit);
        try
        {
            await process.WaitForExitAsync(limit.Token);
            return NotInitialized(new CommandOutcome(process.ExitCode, await output, await error), arguments, workingDirectory);
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

    /// <summary>A command Windows could not initialize says so, instead of leaving its caller to read an empty output as a result.</summary>
    private static CommandOutcome NotInitialized(CommandOutcome outcome, IReadOnlyList<string> arguments, string workingDirectory) =>
        outcome.ExitCode == DllInitFailed
            ? throw new InvalidOperationException(
                $"dotnet {arguments[0]} in {Path.GetFileName(workingDirectory)} did not initialize: Windows ended it with 0xC0000142 (STATUS_DLL_INIT_FAILED) before its first line of code. " +
                "The build under test was never run.")
            : outcome;

    private static ProcessStartInfo StartInfo(IReadOnlyList<string> arguments, string workingDirectory, IReadOnlyDictionary<string, string> environment)
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

        foreach (var (name, value) in environment)
        {
            info.Environment[name] = value;
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
