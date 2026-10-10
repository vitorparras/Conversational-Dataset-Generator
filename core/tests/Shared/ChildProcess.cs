using System.Diagnostics;

namespace Core.Tests.Shared;

/// <summary>Runs a command to completion and returns its exit code and everything it wrote.</summary>
internal static class ChildProcess
{
    /// <summary>The dotnet executable running the tests, so a nested build uses the same SDK.</summary>
    public static string Dotnet => Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";

    /// <summary>
    /// Runs <paramref name="fileName"/> in <paramref name="workingDirectory"/>. Variables in
    /// <paramref name="environment"/> are set on top of the test's own, except MSBuild's.
    /// </summary>
    public static async Task<(int ExitCode, string Output)> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        // The test host runs inside MSBuild's environment; a nested build must not inherit it.
        foreach (var name in Environment.GetEnvironmentVariables().Keys.Cast<string>())
        {
            if (name.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase))
            {
                _ = info.Environment.Remove(name);
            }
        }

        foreach (var (name, value) in environment ?? new Dictionary<string, string?>())
        {
            info.Environment[name] = value;
        }

        using var process = Process.Start(info)!;
        var standardOutput = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return (process.ExitCode, await standardOutput + await standardError);
    }
}
